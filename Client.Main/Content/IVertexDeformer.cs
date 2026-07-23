using Client.Data.Model;
using Microsoft.Xna.Framework;

namespace Client.Main.Content
{
    /// <summary>
    /// Allows objects to procedurally deform skinned vertices during buffer generation.
    /// </summary>
    public interface IVertexDeformer
    {
        Vector3 DeformVertex(in ModelVertex vertex, in Vector3 transformedPosition);
    }
}

