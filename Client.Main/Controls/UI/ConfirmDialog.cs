using Client.Main.Models;
using Client.Main.Controllers;
using Client.Main.Helpers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Client.Main.Controls.UI
{
    /// <summary>
    /// Diálogo de confirmação padrão com OK/Cancel. Herda de PopupFieldDialog para usar
    /// a MESMA caixa/moldura dourada do login (texturas Interface/GFx/popupfield0x.ozd).
    /// Usado pelo botão Exit do menu touch para confirmar a saída do jogo.
    /// </summary>
    public class ConfirmDialog : PopupFieldDialog
    {
        private readonly LabelControl _label;
        private readonly OkCancelButton _okButton;
        private readonly OkCancelButton _cancelButton;
        private static readonly ILogger _logger = MuGame.AppLoggerFactory?.CreateLogger<ConfirmDialog>();

        private Action _onConfirm;

        public string Text
        {
            get => _label.Text;
            set
            {
                _label.Text = value ?? string.Empty;
                AdjustSizeAndLayout();
            }
        }

        private ConfirmDialog()
        {
            Align = ControlAlign.HorizontalCenter | ControlAlign.VerticalCenter;
            AutoViewSize = false;

            _label = new LabelControl
            {
                FontSize = 14f,
                TextColor = Color.White,
                TextAlign = HorizontalAlign.Center,
            };
            Controls.Add(_label);

            _okButton = new OkCancelButton { Text = "OK" };
            _okButton.Click += (s, e) =>
            {
                var cb = _onConfirm;
                Close();
                cb?.Invoke();
            };
            Controls.Add(_okButton);

            _cancelButton = new OkCancelButton { Text = "Cancel" };
            _cancelButton.Click += (s, e) => Close();
            Controls.Add(_cancelButton);

            AdjustSizeAndLayout();
        }

        private void AdjustSizeAndLayout()
        {
            if (_label == null || _okButton == null || _cancelButton == null)
                return;

            int textWidth = _label.ControlSize.X;
            int textHeight = _label.ControlSize.Y;
            int buttonWidth = _okButton.ViewSize.X;
            int buttonHeight = _okButton.ViewSize.Y;
            const int buttonGap = 16;

            int twoButtonsWidth = buttonWidth * 2 + buttonGap;
            // Margem interna maior para a moldura dourada (não colar texto/botões na borda).
            int requiredWidth = Math.Max(textWidth, twoButtonsWidth) + 64;
            int finalWidth = Math.Max(300, requiredWidth);

            int requiredHeight = textHeight + buttonHeight + 80;
            int finalHeight = Math.Max(150, requiredHeight);

            ControlSize = new Point(finalWidth, finalHeight);
            ViewSize = ControlSize;

            _label.X = (finalWidth - textWidth) / 2;
            _label.Y = 36;

            int buttonsY = finalHeight - buttonHeight - 30;
            int startX = (finalWidth - twoButtonsWidth) / 2;
            _okButton.X = startX;
            _okButton.Y = buttonsY;
            _cancelButton.X = startX + buttonWidth + buttonGap;
            _cancelButton.Y = buttonsY;
        }

        /// <summary>
        /// Mostra o diálogo. <paramref name="onConfirm"/> roda ao clicar OK; Cancel só fecha.
        /// </summary>
        public static ConfirmDialog Show(string text, Action onConfirm)
        {
            var scene = MuGame.Instance?.ActiveScene;
            if (scene == null)
            {
                _logger?.LogDebug("[ConfirmDialog.Show] Error: ActiveScene is null.");
                return null;
            }

            foreach (var existing in scene.Controls.OfType<ConfirmDialog>().ToList())
                existing.Close();

            var window = new ConfirmDialog { Text = text, _onConfirm = onConfirm };
            window.ShowDialog();
            window.BringToFront();
            return window;
        }

        public override void Draw(GameTime gameTime)
        {
            if (Status != GameControlStatus.Ready || !Visible)
                return;

            // A moldura/caixa dourada é desenhada pela base (PopupFieldDialog.Draw),
            // que também chama base.Draw → desenha os filhos (label + botões) por cima.
            base.Draw(gameTime);
        }
    }
}
