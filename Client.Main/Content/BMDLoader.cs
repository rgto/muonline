using Client.Data.Model;
using Client.Main.Graphics;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text.Json;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static Client.Main.Core.Utilities.Utils;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Client.Main.Content
{
    public class BMDLoader
    {
        public static BMDLoader Instance { get; } = new BMDLoader();

        private readonly Dictionary<string, Task<ModelAsset>> _bmds = [];
        private readonly Dictionary<ModelAsset, Dictionary<string, string>> _texturePathMap = [];
        private Dictionary<string, Dictionary<int, string>> _blendingConfig;

        private readonly struct MeshCacheKey : IEquatable<MeshCacheKey>
        {
            public MeshCacheKey(int assetId, int meshIndex)
            {
                AssetId = assetId;
                MeshIndex = meshIndex;
            }

            public int AssetId { get; }
            public int MeshIndex { get; }

            public bool Equals(MeshCacheKey other) => AssetId == other.AssetId && MeshIndex == other.MeshIndex;

            public override bool Equals(object obj) => obj is MeshCacheKey other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(AssetId, MeshIndex);
        }

        private static readonly bool DisableGlobalMeshCache =
            Environment.GetEnvironmentVariable("MU_DISABLE_MESH_CACHE") == "1";
        // Enhanced cache state for GetModelBuffers to avoid redundant calculations
        private readonly Dictionary<MeshCacheKey, BufferCacheEntry> _bufferCacheState = [];
        // Per-mesh optimization: track which bones influence a mesh
        private readonly Dictionary<MeshCacheKey, short[]> _meshUsedBones = [];
        // Cache per (asset,mesh) vertex count to avoid per-frame summing
        private readonly Dictionary<MeshCacheKey, int> _meshVertexCountCache = [];
        // Track if index data has been uploaded for this (asset,mesh) so we can skip re-upload
        private readonly HashSet<MeshCacheKey> _indexInitialized = [];

        // Track chosen index element size per mesh (true => 16-bit)
        private readonly Dictionary<MeshCacheKey, bool> _indexIs16Bit = [];
        // Static buffers for GPU skinning path (no per-frame vertex uploads)
        private readonly Dictionary<MeshCacheKey, VertexBuffer> _gpuSkinVertexBuffers = [];
        private readonly Dictionary<MeshCacheKey, IndexBuffer> _gpuSkinIndexBuffers = [];
        private readonly Dictionary<MeshCacheKey, int> _gpuSkinBoneCounts = [];
        private const int ParallelCpuSkinningVertexThreshold = 1200;
        private const int ParallelTriangleAssemblyThreshold = 400;
        private static readonly bool EnableParallelCpuSkinning = Environment.ProcessorCount > 1;
        private static readonly ParallelOptions CpuSkinningParallelOptions = new()
        {
            MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount - 1)
        };

        // Per-frame instrumentation (queried by DebugPanel)
        public int FrameVBUpdates { get; private set; }
        public int FrameIBUploads { get; private set; }
        public int FrameVerticesTransformed { get; private set; }
        public int FrameMeshesProcessed { get; private set; }
        public int FrameCacheHits { get; private set; }
        public int FrameCacheMisses { get; private set; }

        // Snapshot of previous frame (stable for UI)
        public int LastFrameVBUpdates { get; private set; }
        public int LastFrameIBUploads { get; private set; }
        public int LastFrameVerticesTransformed { get; private set; }
        public int LastFrameMeshesProcessed { get; private set; }
        public int LastFrameCacheHits { get; private set; }
        public int LastFrameCacheMisses { get; private set; }

        private struct BufferCacheEntry
        {
            public Color LastColor;
            public int LastBoneMatrixHash;
            public bool IsValid;

            public BufferCacheEntry(Color color, int boneMatrixHash)
            {
                LastColor = color;
                LastBoneMatrixHash = boneMatrixHash;
                IsValid = true;
            }
        }

        private GraphicsDevice _graphicsDevice;
        private ILogger _logger = MuGame.AppLoggerFactory?.CreateLogger<BMDLoader>();

        // for custom blending from json

        private BMDLoader()
        {
            LoadBlendingConfig();
        }

        private void LoadBlendingConfig()
        {
            _blendingConfig = new(StringComparer.OrdinalIgnoreCase);

            try
            {
                var asm = Assembly.GetExecutingAssembly();

                // Looking for exactly one resource ending with the file name
                var resName = asm.GetManifestResourceNames()
                                 .SingleOrDefault(n =>
                                     n.EndsWith("bmd_blending_config.json",
                                                StringComparison.OrdinalIgnoreCase));

                if (resName == null)
                {
                    _logger?.LogWarning(
                        "Embedded resource 'bmd_blending_config.json' not found " +
                        "(check Build Action = Embedded Resource and RootNamespace).");
                    return;
                }

                using var stream = asm.GetManifestResourceStream(resName);
                if (stream == null)
                {
                    _logger?.LogWarning($"Failed to open stream '{resName}'.");
                    return;
                }

                using var reader = new StreamReader(stream);
                var json = reader.ReadToEnd();

                using var doc = JsonDocument.Parse(json);
                var cleanObj = new Dictionary<string, Dictionary<int, string>>(StringComparer.OrdinalIgnoreCase);

                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    if (prop.Name.StartsWith("comment", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var innerDict = new Dictionary<int, string>();
                    foreach (var mesh in prop.Value.EnumerateObject())
                        innerDict[int.Parse(mesh.Name)] = mesh.Value.GetString();

                    cleanObj[prop.Name] = innerDict;
                }

                _blendingConfig = cleanObj;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load embedded BMD blending config.");
            }
        }

        //

        public void SetGraphicsDevice(GraphicsDevice graphicsDevice)
        {
            if (!ReferenceEquals(_graphicsDevice, graphicsDevice))
            {
                DisposeGpuSkinnedBuffers();
            }

            _graphicsDevice = graphicsDevice;
        }

        /// <summary>
        /// Call this at the start of each frame to enable DISCARD/NoOverwrite optimization
        /// </summary>
        public void BeginFrame()
        {
            // Snapshot previous frame for UI stability
            LastFrameVBUpdates = FrameVBUpdates;
            LastFrameIBUploads = FrameIBUploads;
            LastFrameVerticesTransformed = FrameVerticesTransformed;
            LastFrameMeshesProcessed = FrameMeshesProcessed;
            LastFrameCacheHits = FrameCacheHits;
            LastFrameCacheMisses = FrameCacheMisses;

            // Reset counters for the new frame
            FrameVBUpdates = 0;
            FrameIBUploads = 0;
            FrameVerticesTransformed = 0;
            FrameMeshesProcessed = 0;
            FrameCacheHits = 0;
            FrameCacheMisses = 0;
        }

        // XNA Matrix and System.Numerics.Matrix4x4 are layout-identical; ModelSkinning
        // works in System.Numerics (so Client.Data stays MonoGame-free), and the loader
        // converts the palette once per mesh here.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector3 ToXna(in System.Numerics.Vector3 v) => new(v.X, v.Y, v.Z);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static System.Numerics.Matrix4x4 ToNumerics(in Matrix m) => new(
            m.M11, m.M12, m.M13, m.M14,
            m.M21, m.M22, m.M23, m.M24,
            m.M31, m.M32, m.M33, m.M34,
            m.M41, m.M42, m.M43, m.M44);

        private static System.Numerics.Matrix4x4[] ToNumericsPalette(Matrix[] boneMatrix)
        {
            var pal = new System.Numerics.Matrix4x4[boneMatrix.Length];
            for (int i = 0; i < boneMatrix.Length; i++) pal[i] = ToNumerics(in boneMatrix[i]);
            return pal;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector3 FastTransformPosition(in Matrix m, in System.Numerics.Vector3 p)
        {
            // Row-major transform (matching XNA):
            // x' = p.x*m.M11 + p.y*m.M21 + p.z*m.M31 + m.M41, etc.
            return new Vector3(
                p.X * m.M11 + p.Y * m.M21 + p.Z * m.M31 + m.M41,
                p.X * m.M12 + p.Y * m.M22 + p.Z * m.M32 + m.M42,
                p.X * m.M13 + p.Y * m.M23 + p.Z * m.M33 + m.M43);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector3 FastTransformNormal(in Matrix m, in System.Numerics.Vector3 n)
        {
            return new Vector3(
                n.X * m.M11 + n.Y * m.M21 + n.Z * m.M31,
                n.X * m.M12 + n.Y * m.M22 + n.Z * m.M32,
                n.X * m.M13 + n.Y * m.M23 + n.Z * m.M33);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryResolveNormalBoneIndex(Client.Data.Model.ModelMesh mesh, int normalIndex, out int boneIndex)
        {
            boneIndex = 0;
            if (mesh == null || mesh.Normals == null || mesh.Vertices == null ||
                (uint)normalIndex >= (uint)mesh.Normals.Length)
            {
                return false;
            }

            var normal = mesh.Normals[normalIndex];
            if (normal.Node >= 0)
            {
                boneIndex = normal.Node;
                return true;
            }

            int bindVertexIndex = normal.BindVertex;
            if ((uint)bindVertexIndex < (uint)mesh.Vertices.Length)
            {
                short bindVertexBone = mesh.Vertices[bindVertexIndex].Node;
                if (bindVertexBone >= 0)
                {
                    boneIndex = bindVertexBone;
                    return true;
                }
            }

            return false;
        }

        public Task<ModelAsset> Prepare(string path, string textureFolder = null)
        {
            lock (_bmds)
            {
                // Use original path as cache key for embedded resources
                string cacheKey = path;

                // Migration: callers still request ".bmd". Prefer the new ".glb" if it
                // exists, falling back to the original path otherwise. This lets the
                // 350+ callers keep their literal "Monster/NN.bmd" strings unchanged.
                path = ResolveModelPath(path);

                path = GetActualPath(Path.Combine(Constants.DataPath, path));
                if (_bmds.TryGetValue(path, out Task<ModelAsset> modelTask))
                    return modelTask;

                modelTask = LoadAssetAsync(path, textureFolder);
                _bmds.Add(path, modelTask);
                return modelTask;
            }
        }

        /// <summary>
        /// Map a requested model path to the glTF asset. Callers pass legacy ".bmd"
        /// paths; if a sibling ".glb" exists under DataPath we use it, otherwise we
        /// keep the original (so un-converted assets still load during migration).
        /// </summary>
        private static string ResolveModelPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            if (path.EndsWith(".glb", StringComparison.OrdinalIgnoreCase)) return path;

            string glbRel = Path.ChangeExtension(path, ".glb");
            string glbAbs = GetActualPath(Path.Combine(Constants.DataPath, glbRel));
            return File.Exists(glbAbs) ? glbRel : path;
        }

        public Task<bool> AssestExist(string path)
        {
            string finalPath = Path.Combine(Constants.DataPath, path);
            return Task.FromResult(File.Exists(finalPath));
        }
        private async Task<ModelAsset> LoadAssetAsync(string path, string textureFolder = null)
        {
            try
            {
                // 'path' is already resolved to an absolute path in Prepare(); don't re-combine here.

                if (!File.Exists(path))
                {
                    _logger?.LogDebug($"Model not found: {path}");
                    return null;
                }

                // Load is CPU-bound; run off-thread to preserve the async contract.
                // .glb  -> new glTF pipeline (smooth skinning).
                // .bmd  -> legacy bridge (rigid), for assets not yet converted.
                bool isGlb = path.EndsWith(".glb", StringComparison.OrdinalIgnoreCase);
                ModelAsset asset;
                if (isGlb)
                {
                    // SAFETY FALLBACK: a malformed/corrupt .glb (some exports emit an empty-array
                    // node that SharpGLTF rejects) must NOT make the monster invisible. Since the
                    // path was already redirected .bmd->.glb, a throw here would return null and the
                    // object would render nothing. Instead, fall back to the sibling .bmd (the
                    // original rigid model) so the monster still shows up while we fix the export.
                    try
                    {
                        asset = await Task.Run(() => GltfLoader.Load(path));
                    }
                    catch (Exception glbEx)
                    {
                        string bmdPath = Path.ChangeExtension(path, ".bmd");
                        if (File.Exists(bmdPath))
                        {
                            _logger?.LogWarning("glTF load failed for {Path} ({Msg}); falling back to sibling .bmd", path, glbEx.Message);
                            Console.WriteLine($"[GLBFALLBACK] {Path.GetFileName(path)} failed to load ({glbEx.Message}); using .bmd");
                            path = bmdPath;
                            asset = await Task.Run(() => BmdToModelAsset.Load(bmdPath));
                        }
                        else
                        {
                            Console.WriteLine($"[GLBFALLBACK] {Path.GetFileName(path)} failed to load and no sibling .bmd exists: {glbEx.Message}");
                            throw;
                        }
                    }
                }
                else
                {
                    asset = await Task.Run(() => BmdToModelAsset.Load(path));
                }

                // [MODELDIAG] Temporary diagnostic: for glb monsters, dump the rest-pose skinned
                // bounds AS THE CLIENT COMPUTES THEM, so we can see in logcat whether a model that
                // the offline validator calls clean is actually exploding at runtime. Remove once
                // the Lost Tower / Elbeland deformation is diagnosed.
                if (isGlb && asset != null && Constants.MODEL_DIAG && path.Contains("Monster"))
                {
                    try
                    {
                        var pal = Client.Data.Model.ModelSkinning.BuildSkinPalette(asset, 0, 0);
                        var mn = new System.Numerics.Vector3(float.MaxValue);
                        var mx = new System.Numerics.Vector3(float.MinValue);
                        int nanCount = 0, vtot = 0;
                        foreach (var mm in asset.Meshes)
                        {
                            if (mm.Vertices == null) continue;
                            foreach (var vv in mm.Vertices)
                            {
                                var sp = Client.Data.Model.ModelSkinning.SkinPosition(vv, pal);
                                if (float.IsNaN(sp.X) || float.IsNaN(sp.Y) || float.IsNaN(sp.Z)) { nanCount++; continue; }
                                mn = System.Numerics.Vector3.Min(mn, sp);
                                mx = System.Numerics.Vector3.Max(mx, sp);
                                vtot++;
                            }
                        }
                        var sz = mx - mn;
                        var rx = asset.RootTransform;
                        Console.WriteLine($"[MODELDIAG] {System.IO.Path.GetFileName(path)} bones={asset.Bones.Length} verts={vtot} nan={nanCount} restDiag={sz.Length():F1} size=({sz.X:F1},{sz.Y:F1},{sz.Z:F1}) rootScale=({rx.M11:F2},{rx.M22:F2},{rx.M33:F2})");
                    }
                    catch (Exception dex) { Console.WriteLine($"[MODELDIAG] {System.IO.Path.GetFileName(path)} FAILED: {dex.Message}"); }
                }

                // for custom blending from json
                var relativePath = Path.GetRelativePath(Constants.DataPath, path).Replace("\\", "/");
                if (_blendingConfig.TryGetValue(relativePath, out var meshConfig))
                {
                    for (int i = 0; i < asset.Meshes.Length; i++)
                    {
                        if (meshConfig.TryGetValue(i, out var blendMode))
                        {
                            asset.Meshes[i].BlendingMode = blendMode;
                        }
                    }
                }
                //

                var texturePathMap = new Dictionary<string, string>();

                lock (_texturePathMap)
                    _texturePathMap.Add(asset, texturePathMap);

                var dir = !string.IsNullOrEmpty(textureFolder)
                    ? textureFolder
                    : Path.GetRelativePath(Constants.DataPath, Path.GetDirectoryName(path));

                var tasks = new List<Task>();
                foreach (var mesh in asset.Meshes)
                {
                    if (string.IsNullOrEmpty(mesh.TexturePath))
                        continue;

                    // glTF/.glb meshes carry their texture EMBEDDED in the file. Register
                    // those bytes with the loader (decoded lazily) and map the key to
                    // itself — no disk lookup, since there is no loose OZJ/OZT on disk.
                    if (mesh.EmbeddedTextureData != null && mesh.EmbeddedTextureData.Length > 0)
                    {
                        TextureLoader.Instance.RegisterEmbeddedTexture(mesh.TexturePath, mesh.EmbeddedTextureData);
                        texturePathMap.TryAdd(mesh.TexturePath.ToLowerInvariant(), mesh.TexturePath);
                        continue;
                    }

                    // Legacy BMD path: texture is a loose file on disk.
                    var fullPath = Path.Combine(dir, mesh.TexturePath);
                    if (
                        mesh.TexturePath == "unicon.jpg"
                        || mesh.TexturePath == "unicon01.tga"
                    )
                    {
                        fullPath = Path.Combine("Item", mesh.TexturePath);
                    }
                    if (texturePathMap.TryAdd(mesh.TexturePath.ToLowerInvariant(), fullPath))
                        tasks.Add(TextureLoader.Instance.Prepare(fullPath));
                }

                await Task.WhenAll(tasks);

                return asset;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to load asset {path}: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Builds (or updates) the dynamic vertex/index buffers for the given mesh.
        /// Uses ArrayPool to eliminate per‑frame allocations and intelligent caching.
        /// </summary>
        public void GetModelBuffers(
         ModelAsset asset,
         int meshIndex,
         Color color,
         Matrix[] boneMatrix,
         ref DynamicVertexBuffer vertexBuffer,
         ref DynamicIndexBuffer indexBuffer,
         bool skipCache = false,
         IVertexDeformer vertexDeformer = null)
        {
            if (asset == null || boneMatrix == null || _graphicsDevice == null)
            {
                vertexBuffer = null;
                indexBuffer = null;
                return;
            }
            if (meshIndex < 0 || asset.Meshes == null || meshIndex >= asset.Meshes.Length)
            {
                vertexBuffer = null;
                indexBuffer = null;
                return;
            }

            var mesh = asset.Meshes[meshIndex];
            int assetId = RuntimeHelpers.GetHashCode(asset);
            var cacheKey = new MeshCacheKey(assetId, meshIndex);

            if (Environment.GetEnvironmentVariable("MU_BUF_DIAG") == "1" && meshIndex == 0)
                Console.WriteLine($"[BUF-ENTRY] {asset.Name} gltf={asset.IsGltf} m0 skipCache={skipCache}");

            // Use cached vertex count where possible to avoid per-frame summing
            if (!_meshVertexCountCache.TryGetValue(cacheKey, out int totalVertices))
            {
                int vcount = 0;
                var tris = mesh.Triangles;
                for (int i = 0; i < tris.Length; i++) vcount += tris[i].Polygon;
                totalVertices = vcount;
                _meshVertexCountCache[cacheKey] = vcount;
            }
            int totalIndices = totalVertices;
            bool prefer16Bit = totalIndices <= ushort.MaxValue;
            bool useCache = !DisableGlobalMeshCache && !skipCache;

            // Create cache key based on asset and mesh
            // (reusing cacheKey defined above)
            int boneMatrixHash = 0;
            if (useCache)
            {
                // Build or get the set of bones used by this mesh (distinct node indices)
                if (!_meshUsedBones.TryGetValue(cacheKey, out short[] usedBones))
                {
                    var verts = mesh.Vertices;
                    var set = new HashSet<short>();
                    for (int i = 0; i < verts.Length; i++)
                    {
                        short node = verts[i].Node;
                        if (node >= 0)
                            set.Add(node);
                    }

                    var normals = mesh.Normals;
                    for (int i = 0; i < normals.Length; i++)
                    {
                        if (TryResolveNormalBoneIndex(mesh, i, out int normalBone) && normalBone >= 0)
                            set.Add((short)normalBone);
                    }

                    usedBones = set.Count > 0 ? set.ToArray() : Array.Empty<short>();
                    _meshUsedBones[cacheKey] = usedBones;
                }

                // Calculate a hash over only the bones influencing this mesh
                boneMatrixHash = CalculateBoneMatrixHashSubset(boneMatrix, usedBones);

                bool canUseCache = _bufferCacheState.TryGetValue(cacheKey, out var cacheEntry) &&
                                   cacheEntry.IsValid &&
                                   cacheEntry.LastColor == color &&
                                   cacheEntry.LastBoneMatrixHash == boneMatrixHash &&
                                   vertexBuffer != null &&
                                   indexBuffer != null;

                if (Environment.GetEnvironmentVariable("MU_BUF_DIAG") == "1" && asset.IsGltf && meshIndex == 0)
                    Console.WriteLine($"[BUFDIAG-IN] {asset.Name} m0 hash={boneMatrixHash} hit={canUseCache} vb={(vertexBuffer != null)} usedBones={_meshUsedBones[cacheKey].Length}");

                if (canUseCache)
                {
                    FrameCacheHits++;
                    return;
                }

                FrameCacheMisses++;
            }

            FrameMeshesProcessed++;

            // Ensure buffers are properly sized
            if (vertexBuffer != null && vertexBuffer.IsDisposed)
                vertexBuffer = null;

            if (vertexBuffer == null || vertexBuffer.VertexCount < totalVertices)
            {
                DynamicBufferPool.ReturnVertexBuffer(vertexBuffer);
                vertexBuffer = DynamicBufferPool.RentVertexBuffer(totalVertices)
                                 ?? new DynamicVertexBuffer(
                                     _graphicsDevice,
                                     VertexPositionColorNormalTexture.VertexDeclaration,
                                     totalVertices,
                                     BufferUsage.WriteOnly);
            }

            bool createdOrResizedIndex = false;
            bool mismatchIndexSize = false;
            if (_indexIs16Bit.TryGetValue(cacheKey, out bool prevIs16) && prevIs16 != prefer16Bit)
                mismatchIndexSize = true;

            if (indexBuffer != null && indexBuffer.IsDisposed)
            {
                mismatchIndexSize = false; // we'll rent fresh buffer below
                indexBuffer = null;
            }

            if (indexBuffer == null || indexBuffer.IndexCount < totalIndices || mismatchIndexSize)
            {
                DynamicBufferPool.ReturnIndexBuffer(indexBuffer);
                indexBuffer = DynamicBufferPool.RentIndexBuffer(totalIndices, prefer16Bit)
                              ?? new DynamicIndexBuffer(
                                  _graphicsDevice,
                                  prefer16Bit ? IndexElementSize.SixteenBits : IndexElementSize.ThirtyTwoBits,
                                  totalIndices,
                                  BufferUsage.WriteOnly);
                createdOrResizedIndex = true;
                _indexIs16Bit[cacheKey] = prefer16Bit;
            }

            // Build vertex data with unique-vertex transform caching
            VertexPositionColorNormalTexture[] vertices = null;
            Vector3[] posCache = null;
            Vector3[] normalCache = null;
            bool[] visited = null;
            bool[] normalVisited = null;
            int[] triangleOffsets = null;
            ITexCoordDeformer texCoordDeformer = vertexDeformer as ITexCoordDeformer;

            try
            {
                vertices = ArrayPool<VertexPositionColorNormalTexture>.Shared.Rent(totalVertices);
                posCache = ArrayPool<Vector3>.Shared.Rent(mesh.Vertices.Length);
                normalCache = ArrayPool<Vector3>.Shared.Rent(mesh.Normals.Length);
                bool useParallelTransform = EnableParallelCpuSkinning &&
                                            vertexDeformer == null &&
                                            mesh.Vertices.Length >= ParallelCpuSkinningVertexThreshold;
                bool useParallelAssembly = useParallelTransform &&
                                           mesh.Triangles.Length >= ParallelTriangleAssemblyThreshold;

                if (!useParallelTransform)
                {
                    visited = ArrayPool<bool>.Shared.Rent(mesh.Vertices.Length);
                    normalVisited = ArrayPool<bool>.Shared.Rent(mesh.Normals.Length);
                    Array.Clear(visited, 0, mesh.Vertices.Length);
                    Array.Clear(normalVisited, 0, mesh.Normals.Length);
                }

                int v = 0;
                int uniqueTransformed = 0;

                // Convert the XNA palette to System.Numerics once (ModelSkinning is
                // MonoGame-free). Reused by every skinning site below.
                var skinPalette = ToNumericsPalette(boneMatrix);

                if (useParallelTransform)
                {
                    var meshVertices = mesh.Vertices;

                    Parallel.For(0, meshVertices.Length, CpuSkinningParallelOptions, vi =>
                    {
                        // Weighted (smooth) skinning: blend up to 4 bone influences.
                        var vert = meshVertices[vi];
                        posCache[vi] = ToXna(ModelSkinning.SkinPosition(in vert, skinPalette));
                        // Normal shares the vertex's influences in the unified layout.
                        normalCache[vi] = ToXna(ModelSkinning.SkinNormal(in vert, skinPalette));
                    });

                    uniqueTransformed = meshVertices.Length;
                }

                // Diag dev-only (MU_BUF_DIAG=1): bbox dos vértices REALMENTE enviados
                // à GPU por este rebuild (compara caminhos DLS on/off).
                if (Environment.GetEnvironmentVariable("MU_BUF_DIAG") == "1")
                {
                    var bmn = new Vector3(float.MaxValue); var bmx = new Vector3(float.MinValue);
                    if (useParallelTransform)
                    {
                        for (int vi = 0; vi < mesh.Vertices.Length; vi++)
                        { bmn = Vector3.Min(bmn, posCache[vi]); bmx = Vector3.Max(bmx, posCache[vi]); }
                    }
                    else
                    {
                        foreach (var vert in mesh.Vertices)
                        {
                            var pp = ToXna(ModelSkinning.SkinPosition(in vert, skinPalette));
                            bmn = Vector3.Min(bmn, pp); bmx = Vector3.Max(bmx, pp);
                        }
                    }
                    var bs = bmx - bmn;
                    string boneInfo = "";
                    if (_meshUsedBones.TryGetValue(cacheKey, out var ub) && ub.Length == 1 && ub[0] < skinPalette.Length)
                    {
                        var pm = skinPalette[ub[0]];
                        float sc = new System.Numerics.Vector3(pm.M11, pm.M12, pm.M13).Length();
                        boneInfo = $" bone={ub[0]} palScale={sc:F2} palT=({pm.M41:F0},{pm.M42:F0},{pm.M43:F0})";
                        var rv = mesh.Vertices.Length > 0 ? mesh.Vertices[0].Position : default;
                        boneInfo += $" v0=({rv.X:F1},{rv.Y:F1},{rv.Z:F1})";
                    }
                    Console.WriteLine($"[BUFDIAG] {asset.Name} m{meshIndex} size=({bs.X:F0},{bs.Y:F0},{bs.Z:F0}) zmin={bmn.Z:F0} dls={Constants.ENABLE_DYNAMIC_LIGHTING_SHADER}{boneInfo}");
                }

                if (useParallelAssembly)
                {
                    // Phase 2: assemble final vertex stream in parallel. Every triangle
                    // writes to its own contiguous output range, so there are no races.
                    int triCount = mesh.Triangles.Length;
                    triangleOffsets = ArrayPool<int>.Shared.Rent(triCount);
                    int offset = 0;
                    for (int i = 0; i < triCount; i++)
                    {
                        triangleOffsets[i] = offset;
                        offset += mesh.Triangles[i].Polygon;
                    }

                    Parallel.For(0, triCount, CpuSkinningParallelOptions, triIndex =>
                    {
                        var tri = mesh.Triangles[triIndex];
                        int dst = triangleOffsets[triIndex];

                        for (int j = 0; j < tri.Polygon; j++)
                        {
                            int vi = tri.VertexIndex[j];
                            int ni = tri.NormalIndex[j];
                            int ti = tri.TexCoordIndex[j];

                            var normal = normalCache[ni];
                            var uv = mesh.TexCoords[ti];

                            vertices[dst + j] = new VertexPositionColorNormalTexture(
                                posCache[vi],
                                color,
                                normal,
                                new Vector2(uv.U, uv.V));
                        }
                    });
                }
                else
                {
                    foreach (var tri in mesh.Triangles)
                    {
                        for (int j = 0; j < tri.Polygon; j++)
                        {
                            int vi = tri.VertexIndex[j];

                            if (!useParallelTransform && !visited[vi])
                            {
                                visited[vi] = true;
                                uniqueTransformed++;
                                var vert = mesh.Vertices[vi];

                                // Weighted (smooth) skinning of position + normal. In the
                                // unified layout the normal shares the vertex's influences,
                                // so normal index == vertex index (ni == vi).
                                posCache[vi] = ToXna(ModelSkinning.SkinPosition(in vert, skinPalette));
                                normalCache[vi] = ToXna(ModelSkinning.SkinNormal(in vert, skinPalette));

                                if (vertexDeformer != null)
                                {
                                    posCache[vi] = vertexDeformer.DeformVertex(in vert, in posCache[vi]);
                                }
                            }

                            var normal = normalCache[vi];

                            int ti = tri.TexCoordIndex[j];
                            var uv = mesh.TexCoords[ti];

                            Vector2 texCoord = texCoordDeformer != null
                                ? texCoordDeformer.DeformTexCoord(uv.U, uv.V)
                                : new Vector2(uv.U, uv.V);

                            vertices[v] = new VertexPositionColorNormalTexture(
                                posCache[vi],
                                color,
                                normal,
                                texCoord);
                            v++;
                        }
                    }
                }

                // Always discard the previous contents because we rewrite the whole buffer each time.
                // Using NoOverwrite was causing DX to reuse in-flight data -> animated meshes glitch.
                vertexBuffer.SetData(vertices, 0, totalVertices, SetDataOptions.Discard);
                FrameVBUpdates++;
                FrameVerticesTransformed += uniqueTransformed;

                // Upload index data only if needed (new or resized buffer or not yet initialized)
                if (createdOrResizedIndex || !_indexInitialized.Contains(cacheKey))
                {
                    if (prefer16Bit)
                    {
                        var indices16 = ArrayPool<ushort>.Shared.Rent(totalIndices);
                        try
                        {
                            for (int i = 0; i < totalIndices; i++) indices16[i] = (ushort)i;
                            indexBuffer.SetData(indices16, 0, totalIndices, SetDataOptions.Discard);
                        }
                        finally
                        {
                            ArrayPool<ushort>.Shared.Return(indices16, clearArray: true);
                        }
                    }
                    else
                    {
                        var indices32 = ArrayPool<int>.Shared.Rent(totalIndices);
                        try
                        {
                            for (int i = 0; i < totalIndices; i++) indices32[i] = i;
                            indexBuffer.SetData(indices32, 0, totalIndices, SetDataOptions.Discard);
                        }
                        finally
                        {
                            ArrayPool<int>.Shared.Return(indices32, clearArray: true);
                        }
                    }

                    _indexInitialized.Add(cacheKey);
                    FrameIBUploads++;
                }

                // Update cache entry only if caching is enabled for this platform
                if (!skipCache && !DisableGlobalMeshCache)
                {
                    _bufferCacheState[cacheKey] = new BufferCacheEntry(color, boneMatrixHash);
                }
            }
            finally
            {
                if (vertices != null)
                {
                    ArrayPool<VertexPositionColorNormalTexture>.Shared.Return(vertices);
                }

                if (posCache != null)
                {
                    ArrayPool<Vector3>.Shared.Return(posCache);
                }

                if (normalCache != null)
                {
                    ArrayPool<Vector3>.Shared.Return(normalCache);
                }

                if (visited != null)
                {
                    ArrayPool<bool>.Shared.Return(visited, clearArray: true);
                }

                if (normalVisited != null)
                {
                    ArrayPool<bool>.Shared.Return(normalVisited, clearArray: true);
                }

                if (triangleOffsets != null)
                {
                    ArrayPool<int>.Shared.Return(triangleOffsets, clearArray: false);
                }
            }
        }

        /// <summary>
        /// Returns immutable mesh buffers for GPU skinning path.
        /// Buffers store bind-pose positions and per-vertex bone index.
        /// </summary>
        public bool TryGetGpuSkinnedMeshBuffers(
            ModelAsset asset,
            int meshIndex,
            out VertexBuffer vertexBuffer,
            out IndexBuffer indexBuffer,
            out int boneCount)
        {
            vertexBuffer = null;
            indexBuffer = null;
            boneCount = 0;

            if (asset == null || _graphicsDevice == null || asset.Meshes == null ||
                meshIndex < 0 || meshIndex >= asset.Meshes.Length)
            {
                return false;
            }

            int assetId = RuntimeHelpers.GetHashCode(asset);
            var cacheKey = new MeshCacheKey(assetId, meshIndex);

            if (_gpuSkinVertexBuffers.TryGetValue(cacheKey, out var cachedVB) &&
                _gpuSkinIndexBuffers.TryGetValue(cacheKey, out var cachedIB) &&
                _gpuSkinBoneCounts.TryGetValue(cacheKey, out var cachedBoneCount) &&
                cachedVB != null && !cachedVB.IsDisposed &&
                cachedIB != null && !cachedIB.IsDisposed)
            {
                vertexBuffer = cachedVB;
                indexBuffer = cachedIB;
                boneCount = cachedBoneCount;
                return true;
            }

            var mesh = asset.Meshes[meshIndex];
            if (mesh?.Triangles == null || mesh.Vertices == null || mesh.Normals == null || mesh.TexCoords == null)
                return false;

            int totalVertices = 0;
            var triangles = mesh.Triangles;
            for (int i = 0; i < triangles.Length; i++)
                totalVertices += triangles[i].Polygon;

            if (totalVertices <= 0)
                return false;

            bool prefer16Bit = totalVertices <= ushort.MaxValue;
            var vertices = ArrayPool<SkinnedVertexPositionColorNormalTexture>.Shared.Rent(totalVertices);

            try
            {
              try
              {
                int maxBoneIndex = 0;
                int v = 0;

                for (int triIdx = 0; triIdx < triangles.Length; triIdx++)
                {
                    var tri = triangles[triIdx];
                    for (int j = 0; j < tri.Polygon; j++)
                    {
                        int vi = tri.VertexIndex[j];
                        int ni = tri.NormalIndex[j];
                        int ti = tri.TexCoordIndex[j];

                        var vert = mesh.Vertices[vi];
                        int positionBoneIndex = vert.Node >= 0 ? vert.Node : 0;
                        int normalBoneIndex = positionBoneIndex;
                        if (TryResolveNormalBoneIndex(mesh, ni, out int resolvedNormalBone) && resolvedNormalBone >= 0)
                            normalBoneIndex = resolvedNormalBone;

                        if (positionBoneIndex > maxBoneIndex)
                            maxBoneIndex = positionBoneIndex;
                        if (normalBoneIndex > maxBoneIndex)
                            maxBoneIndex = normalBoneIndex;

                        var normal = mesh.Normals[ni].Normal;
                        var uv = mesh.TexCoords[ti];

                        vertices[v++] = new SkinnedVertexPositionColorNormalTexture(
                            vert.Position,
                            Color.White,
                            normal,
                            new Vector2(uv.U, uv.V),
                            new Vector2(positionBoneIndex, normalBoneIndex));
                    }
                }

                var newVB = new VertexBuffer(
                    _graphicsDevice,
                    SkinnedVertexPositionColorNormalTexture.VertexDeclaration,
                    totalVertices,
                    BufferUsage.WriteOnly);
                newVB.SetData(vertices, 0, totalVertices);

                IndexBuffer newIB;
                if (prefer16Bit)
                {
                    var indices16 = ArrayPool<ushort>.Shared.Rent(totalVertices);
                    try
                    {
                        for (int i = 0; i < totalVertices; i++)
                            indices16[i] = (ushort)i;

                        newIB = new IndexBuffer(
                            _graphicsDevice,
                            IndexElementSize.SixteenBits,
                            totalVertices,
                            BufferUsage.WriteOnly);
                        newIB.SetData(indices16, 0, totalVertices);
                    }
                    finally
                    {
                        ArrayPool<ushort>.Shared.Return(indices16, clearArray: true);
                    }
                }
                else
                {
                    var indices32 = ArrayPool<int>.Shared.Rent(totalVertices);
                    try
                    {
                        for (int i = 0; i < totalVertices; i++)
                            indices32[i] = i;

                        newIB = new IndexBuffer(
                            _graphicsDevice,
                            IndexElementSize.ThirtyTwoBits,
                            totalVertices,
                            BufferUsage.WriteOnly);
                        newIB.SetData(indices32, 0, totalVertices);
                    }
                    finally
                    {
                        ArrayPool<int>.Shared.Return(indices32, clearArray: true);
                    }
                }

                if (_gpuSkinVertexBuffers.TryGetValue(cacheKey, out var oldVB))
                    oldVB?.Dispose();
                if (_gpuSkinIndexBuffers.TryGetValue(cacheKey, out var oldIB))
                    oldIB?.Dispose();

                _gpuSkinVertexBuffers[cacheKey] = newVB;
                _gpuSkinIndexBuffers[cacheKey] = newIB;
                _gpuSkinBoneCounts[cacheKey] = maxBoneIndex + 1;

                vertexBuffer = newVB;
                indexBuffer = newIB;
                boneCount = maxBoneIndex + 1;
                return true;
              }
              catch (Exception ex)
              {
                // GPU skin buffer build failed for this mesh — log and fall back to CPU
                // skinning (return false) instead of taking down the process. Guards models
                // with unusual structure (e.g. FBX2glTF multi-skin monsters).
                System.Console.WriteLine($"[GPUSKIN] build failed asset={asset.Name} mesh={meshIndex} verts={totalVertices} bones={asset.Bones?.Length}: {ex.GetType().Name} {ex.Message}");
                vertexBuffer = null; indexBuffer = null; boneCount = 0;
                return false;
              }
            }
            finally
            {
                ArrayPool<SkinnedVertexPositionColorNormalTexture>.Shared.Return(vertices);
            }
        }

        private int CalculateBoneMatrixHashSubset(Matrix[] boneMatrix, short[] usedBones)
        {
            if (boneMatrix == null || usedBones == null || usedBones.Length == 0) return 0;
            int hash = 17;
            for (int i = 0; i < usedBones.Length; i++)
            {
                int idx = usedBones[i];
                if ((uint)idx >= (uint)boneMatrix.Length) continue;
                ref var m = ref boneMatrix[idx];
                hash = hash * 31 + m.Translation.GetHashCode();
                // Include more rotation/scale components to reduce false cache hits
                hash = hash * 31 + m.M11.GetHashCode();
                hash = hash * 31 + m.M12.GetHashCode();
                hash = hash * 31 + m.M13.GetHashCode();
                hash = hash * 31 + m.M21.GetHashCode();
                hash = hash * 31 + m.M22.GetHashCode();
                hash = hash * 31 + m.M23.GetHashCode();
                hash = hash * 31 + m.M31.GetHashCode();
                hash = hash * 31 + m.M32.GetHashCode();
                hash = hash * 31 + m.M33.GetHashCode();
            }
            return hash;
        }

        public string GetTexturePath(ModelAsset bmd, string texturePath)
        {
            texturePath = texturePath.ToLowerInvariant();

            string result = null;

            if (_texturePathMap.TryGetValue(bmd, out Dictionary<string, string> value) && value.TryGetValue(texturePath, out string fullTexturePath))
                result = fullTexturePath;

            if (result == null)
                _logger?.LogDebug($"Texture path not found: {texturePath}");

            return result;
        }

        // Clear cache when needed (e.g., when objects are disposed)
        public void ClearBufferCache()
        {
            _bufferCacheState.Clear();
            _indexInitialized.Clear();
            _indexIs16Bit.Clear();
            DisposeGpuSkinnedBuffers();
        }

        private void DisposeGpuSkinnedBuffers()
        {
            foreach (var vb in _gpuSkinVertexBuffers.Values)
            {
                vb?.Dispose();
            }

            foreach (var ib in _gpuSkinIndexBuffers.Values)
            {
                ib?.Dispose();
            }

            _gpuSkinVertexBuffers.Clear();
            _gpuSkinIndexBuffers.Clear();
            _gpuSkinBoneCounts.Clear();
        }
    }

}
