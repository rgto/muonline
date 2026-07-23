using Client.Main.Content;
using Client.Main.Controllers;
using Client.Main.Helpers;
using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Client.Main.Controls.UI
{
    public enum TextFieldSkin
    {
        Flat,
        NineSlice
    }

    public class TextFieldControl : UIControl, IUiTexturePreloadable
    {
        public static Type ControlType = typeof(TextFieldControl);

        public static TextFieldControl Create()
        {
            return (TextFieldControl)Activator.CreateInstance(ControlType, true);
        }

        protected readonly StringBuilder _inputText = new();
        private double _cursorBlinkTimer;
        private bool _showCursor;
        private float _scrollOffset;

        private const int TextMargin = 5;
        private const int CursorBlinkInterval = 500;

        private Texture2D[] _nineSlice = new Texture2D[9];
        private static readonly string[] s_nineSliceSuffixes =
        {
            "01", "02", "03", "04", "05", "06", "07", "08", "09"
        };

        private static readonly ILogger _logger = MuGame.AppLoggerFactory?.CreateLogger<TextFieldControl>();

        public TextFieldSkin Skin { get; set; } = TextFieldSkin.Flat;
        public Color TextColor { get; set; } = Color.White;
        public float FontSize { get; set; } = 12f;
        public bool IsFocused { get; private set; }
        public string Label { get; set; }
        public string Placeholder { get; set; }

        /// <summary>
        /// Filtro opcional aplicado a TODO texto que entra no campo — inclusive o que volta
        /// do teclado do sistema no Android (que não passa pelo OnTextInput). Nulo = sem
        /// filtro. O campo de NOME usa isto pra barrar espaço/quebra de linha.
        /// </summary>
        public Func<string, string> Sanitizer { get; set; }

        public string Value
        {
            get => _inputText.ToString();
            set
            {
                var v = value ?? string.Empty;
                if (Sanitizer != null) v = Sanitizer(v);
                _inputText.Clear();
                _inputText.Append(v);
                UpdateScrollOffset();
                MoveCursorToEnd();
            }
        }

        public bool MaskValue { get; set; }
        public event EventHandler ValueChanged;
        public event EventHandler EnterKeyPressed;

        protected TextFieldControl()
        {
            AutoViewSize = false;
            ViewSize = new Point(176, 14);
            Interactive = true;
            IsFocused = false;
        }

        public IEnumerable<string> GetPreloadTexturePaths()
        {
            if (Skin != TextFieldSkin.NineSlice)
                yield break;

            for (int i = 0; i < s_nineSliceSuffixes.Length; i++)
            {
                yield return $"Interface/GFx/textbg{s_nineSliceSuffixes[i]}.ozd";
            }
        }

        public override async Task Load()
        {
            await base.Load();

            if (Skin == TextFieldSkin.NineSlice)
            {
                for (int i = 0; i < s_nineSliceSuffixes.Length; i++)
                {
                    _nineSlice[i] = await TextureLoader.Instance.PrepareAndGetTexture($"Interface/GFx/textbg{s_nineSliceSuffixes[i]}.ozd");
                }
            }
        }

        public override void OnFocus()
        {
            if (IsFocused) return;
            base.OnFocus();
            IsFocused = true;
            _showCursor = true;
            _cursorBlinkTimer = 0;
            if (Scene != null) Scene.FocusControl = this;

            _logger?.LogDebug("TextFieldControl: OnFocus called. Subscribing to TextInput.");
        }

        public override void OnBlur()
        {
            if (!IsFocused) return;
            base.OnBlur();
            IsFocused = false;
            _showCursor = false;
            _cursorBlinkTimer = 0;

            _logger?.LogDebug("TextFieldControl: OnBlur called. Unsubscribing from TextInput.");

#if ANDROID
            AndroidKeyboard.TextInput -= OnTextInput;
            AndroidKeyboard.Hide();
#endif
        }

        public new void Focus() => OnFocus();
        public new void Blur() => OnBlur();

        public void MoveCursorToEnd()
        {
            UpdateScrollOffset();
            if (IsFocused)
            {
                _showCursor = true;
                _cursorBlinkTimer = 0;
            }
        }

        protected void UpdateScrollOffset()
        {
            if (GraphicsManager.Instance?.Font == null) return;

            float scaleFactor = FontSize / Constants.BASE_FONT_SIZE;
            var textToDisplay = MaskValue ? new string('*', _inputText.Length) : _inputText.ToString();
            var textWidth = GraphicsManager.Instance.Font.MeasureString(textToDisplay).X * scaleFactor;
            float maxVisibleWidth = DisplayRectangle.Width - TextMargin * 2;

            _scrollOffset = textWidth > maxVisibleWidth ? textWidth - maxVisibleWidth : 0;
        }

        protected void OnEnterKeyPressed()
        {
            EnterKeyPressed?.Invoke(this, EventArgs.Empty);
        }

        protected void OnValueChanged()
        {
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Handles text input on Android (from soft keyboard or scrcpy).
        /// </summary>
#if ANDROID
        private void OnTextInput(object sender, Platform.Android.TextInputEventArgs e)
        {
            bool textChanged = false;

            // Handle control keys by character or key code
            if (e.Character == '\r' || e.Key == Keys.Enter)
            {
                EnterKeyPressed?.Invoke(this, EventArgs.Empty);
                ValueChanged?.Invoke(this, EventArgs.Empty);
                return; // Enter usually consumes the event
            }
            else if (e.Character == '\b' || e.Key == Keys.Back)
            {
                // Backspace - delete last character
                if (_inputText.Length > 0)
                {
                    _inputText.Remove(_inputText.Length - 1, 1);
                    textChanged = true;
                }
            }
            else if (e.Character != '\0' && !char.IsControl(e.Character))
            {
                // Standard printable character input
                _inputText.Append(e.Character);
                textChanged = true;
            }

            if (textChanged)
            {
                UpdateScrollOffset();
                MoveCursorToEnd();
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }
#endif

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (!IsFocused || !Visible) return;

#if !ANDROID
            // On non-Android platforms (Windows, Linux, Mac), use keyboard polling
            var keysPressed = MuGame.Instance.Keyboard.GetPressedKeys();
            bool shift = MuGame.Instance.Keyboard.IsKeyDown(Keys.LeftShift) || MuGame.Instance.Keyboard.IsKeyDown(Keys.RightShift);
            bool capsLock = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows) ? Console.CapsLock : false;

            bool textModifiedByKey = false;
            foreach (var key in keysPressed)
            {
                if (MuGame.Instance.PrevKeyboard.IsKeyUp(key))
                {
                    ProcessKey(key, shift, capsLock);
                    textModifiedByKey = true;
                }
            }

            if (textModifiedByKey || (IsFocused && !MuGame.Instance.PrevKeyboard.GetPressedKeys().Any()))
            {
                UpdateScrollOffset();
            }
#endif

            _cursorBlinkTimer += gameTime.ElapsedGameTime.TotalMilliseconds;
            if (_cursorBlinkTimer >= CursorBlinkInterval)
            {
                _showCursor = !_showCursor;
                _cursorBlinkTimer = 0;
            }
        }

#if !ANDROID
        // Keyboard input processing for Windows/Desktop platforms
        private void ProcessKey(Keys key, bool shift, bool capsLock)
        {
            bool textChanged = false;
            if (key == Keys.Back && _inputText.Length > 0)
            {
                _inputText.Remove(_inputText.Length - 1, 1);
                textChanged = true;
            }
            else if (key == Keys.Enter)
            {
                EnterKeyPressed?.Invoke(this, EventArgs.Empty);
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                char character = KeyToChar(key, shift, capsLock);
                if (character != '\0')
                {
                    _inputText.Append(character);
                    textChanged = true;
                }
            }

            if (textChanged)
            {
                UpdateScrollOffset();
                MoveCursorToEnd();
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private char KeyToChar(Keys key, bool shift, bool capsLock)
        {
            if (key >= Keys.A && key <= Keys.Z)
            {
                bool isUpper = capsLock ^ shift;
                char letter = (char)('A' + (key - Keys.A));
                return isUpper ? letter : char.ToLower(letter);
            }
            else if (key >= Keys.D0 && key <= Keys.D9)
            {
                char digit = (char)('0' + (key - Keys.D0));
                if (shift)
                {
                    return key switch
                    {
                        Keys.D1 => '!',
                        Keys.D2 => '@',
                        Keys.D3 => '#',
                        Keys.D4 => '$',
                        Keys.D5 => '%',
                        Keys.D6 => '^',
                        Keys.D7 => '&',
                        Keys.D8 => '*',
                        Keys.D9 => '(',
                        Keys.D0 => ')',
                        _ => digit,
                    };
                }
                return digit;
            }
            else if (key >= Keys.NumPad0 && key <= Keys.NumPad9)
            {
                return (char)('0' + (key - Keys.NumPad0));
            }
            return key switch
            {
                Keys.Space => ' ',
                Keys.OemComma => ',',
                Keys.OemPeriod => '.',
                Keys.OemMinus => shift ? '_' : '-',
                Keys.OemPlus => shift ? '+' : '=',
                Keys.OemQuestion => shift ? '?' : '/',
                Keys.OemOpenBrackets => shift ? '{' : '[',
                Keys.OemCloseBrackets => shift ? '}' : ']',
                Keys.OemPipe => shift ? '|' : '\\',
                Keys.OemTilde => shift ? '~' : '`',
                Keys.OemQuotes => shift ? '"' : '\'',
                Keys.OemSemicolon => shift ? ':' : ';',
                _ => '\0'
            };
        }
#endif

        public override void Draw(GameTime gameTime)
        {
            if (Status != GameControlStatus.Ready || !Visible)
                return;

            using (new SpriteBatchScope(
                GraphicsManager.Instance.Sprite,
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                transform: UiScaler.SpriteTransform))
            {
                var spriteBatch = GraphicsManager.Instance.Sprite;

                if (Skin == TextFieldSkin.NineSlice && _nineSlice[0] != null)
                    DrawNineSliceBackground(spriteBatch);
                else
                    DrawFlatBackground(spriteBatch);

                DrawTextAndCursor(spriteBatch);
            }

            base.Draw(gameTime);
        }

        private void DrawFlatBackground(SpriteBatch spriteBatch)
        {
            // Desenha fundo + borda DIRETO aqui (antes do texto). NÃO usa DrawBackground()/
            // DrawBorder() do base, senão o base.Draw() (GameControl.Draw) os redesenharia
            // POR CIMA do texto — era o bug do "texto por baixo". Por isso BackgroundColor/
            // BorderColor ficam Transparent (base não redesenha nada).
            var pixel = GraphicsManager.Instance.Pixel;
            var r = DisplayRectangle;

            if (FlatBackgroundColor.A > 0)
                spriteBatch.Draw(pixel, r, FlatBackgroundColor * Alpha);

            if (FlatBorderThickness > 0 && FlatBorderColor.A > 0)
            {
                var bc = FlatBorderColor * Alpha;
                int t = FlatBorderThickness;
                spriteBatch.Draw(pixel, new Rectangle(r.X, r.Y, r.Width, t), bc);
                spriteBatch.Draw(pixel, new Rectangle(r.X, r.Bottom - t, r.Width, t), bc);
                spriteBatch.Draw(pixel, new Rectangle(r.X, r.Y, t, r.Height), bc);
                spriteBatch.Draw(pixel, new Rectangle(r.Right - t, r.Y, t, r.Height), bc);
            }
        }

        /// <summary>Fundo do skin Flat, desenhado ANTES do texto (não pelo base).</summary>
        public Color FlatBackgroundColor { get; set; } = Color.Transparent;
        public Color FlatBorderColor { get; set; } = Color.Transparent;
        public int FlatBorderThickness { get; set; } = 0;

        private void DrawNineSliceBackground(SpriteBatch spriteBatch)
        {
            var r = DisplayRectangle;

            var TL = _nineSlice[0];
            var T = _nineSlice[1];
            var TR = _nineSlice[2];
            var L = _nineSlice[3];
            var C = _nineSlice[4];
            var R = _nineSlice[5];
            var BL = _nineSlice[6];
            var B = _nineSlice[7];
            var BR = _nineSlice[8];

            spriteBatch.Draw(TL, new Rectangle(r.X, r.Y, TL.Width, TL.Height), Color.White);
            spriteBatch.Draw(TR, new Rectangle(r.Right - TR.Width, r.Y, TR.Width, TR.Height), Color.White);
            spriteBatch.Draw(BL, new Rectangle(r.X, r.Bottom - BL.Height, BL.Width, BL.Height), Color.White);
            spriteBatch.Draw(BR, new Rectangle(r.Right - BR.Width, r.Bottom - BR.Height, BR.Width, BR.Height), Color.White);

            spriteBatch.Draw(T, new Rectangle(r.X + TL.Width, r.Y, r.Width - TL.Width - TR.Width, T.Height), Color.White);
            spriteBatch.Draw(B, new Rectangle(r.X + BL.Width, r.Bottom - B.Height, r.Width - BL.Width - BR.Width, B.Height), Color.White);
            spriteBatch.Draw(L, new Rectangle(r.X, r.Y + TL.Height, L.Width, r.Height - TL.Height - BL.Height), Color.White);
            spriteBatch.Draw(R, new Rectangle(r.Right - R.Width, r.Y + TR.Height, R.Width, r.Height - TR.Height - BR.Height), Color.White);

            spriteBatch.Draw(C, new Rectangle(r.X + L.Width, r.Y + T.Height, r.Width - L.Width - R.Width, r.Height - T.Height - B.Height), Color.White);
        }

        private void DrawTextAndCursor(SpriteBatch spriteBatch)
        {
            var font = GraphicsManager.Instance.Font;
            if (font == null) return;

            var gd = GraphicsManager.Instance.GraphicsDevice;
            var originalScissorRect = gd.ScissorRectangle;
            // O texto é desenhado com UiScaler.SpriteTransform (matriz de escala), mas o
            // scissor test é em PIXELS DE TELA — então a área de recorte precisa ser
            // convertida de coords virtuais pra reais (ToActual). Sem isso o recorte caía
            // no Y errado e o texto aparecia acima/atrás do input.
            var areaVirtual = new Rectangle(
                DisplayRectangle.X + TextMargin,
                DisplayRectangle.Y,
                Math.Max(0, DisplayRectangle.Width - TextMargin * 2),
                DisplayRectangle.Height
            );
            var area = UiScaler.ToActual(areaVirtual);
            gd.ScissorRectangle = Rectangle.Intersect(originalScissorRect, area);
            gd.RasterizerState = new RasterizerState { ScissorTestEnable = true };

            float scale = FontSize / Constants.BASE_FONT_SIZE;
            string text = MaskValue ? new string('*', _inputText.Length) : _inputText.ToString();
            // MeasureString(...).Y é a ALTURA DE LINHA cheia (inclui ascendente, descendente
            // e line-gap), não a altura visível do glifo. Centralizar por ela deixa o texto
            // "alto" na caixa. A tinta visível é ~72% da linha e começa ~14% abaixo do topo,
            // então centramos pela altura VISÍVEL e compensamos o offset do topo.
            float lineH = font.MeasureString("Ay").Y * scale;
            float inkH = lineH * 0.72f;
            float inkTopOffset = lineH * 0.14f;
            Vector2 textPos = new Vector2(DisplayRectangle.X + TextMargin - _scrollOffset,
                                          DisplayRectangle.Y + (DisplayRectangle.Height - inkH) / 2f - inkTopOffset);

            spriteBatch.DrawString(font, text, textPos, TextColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

            if (IsFocused && _showCursor)
            {
                float w = font.MeasureString(text).X * scale;
                var cursorPos = textPos + new Vector2(w, 0);
                if (cursorPos.X >= areaVirtual.Left && cursorPos.X <= areaVirtual.Right)
                {
                    spriteBatch.DrawString(font, "|", cursorPos, TextColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                }
            }

            gd.ScissorRectangle = originalScissorRect;
            gd.RasterizerState = RasterizerState.CullNone;
        }
    }
}
