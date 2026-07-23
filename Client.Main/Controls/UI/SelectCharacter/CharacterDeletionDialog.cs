#nullable enable
using Client.Main.Models;
using Microsoft.Xna.Framework;
using System;

namespace Client.Main.Controls.UI.SelectCharacter
{
    /// <summary>
    /// Confirmação de exclusão de personagem — MESMO visual do login (PopupFieldDialog
    /// com moldura dourada): título, mensagem, "Security Code:", 1 input e os botões
    /// OK/Cancel (arte ok_cancel.OZT). Layout editável via DeleteLayout (hud-edit-delete).
    /// </summary>
    public class CharacterDeletionDialog : PopupFieldDialog
    {
        private readonly string _characterName;
        private readonly TextFieldControl _codeInput;
        private readonly OkCancelButton _okButton;
        private readonly OkCancelButton _cancelButton;

        public event EventHandler<string>? DeleteConfirmed;
        public event EventHandler? CancelRequested;

        public CharacterDeletionDialog(string characterName)
        {
            _characterName = characterName;

            ControlSize = new Point(DeleteLayout.PanelW, DeleteLayout.PanelH);
            Align = ControlAlign.HorizontalCenter | ControlAlign.VerticalCenter;
            Interactive = true;

            // Título "DELETE CHARACTER"
            Controls.Add(new LabelControl
            {
                Text = "DELETE CHARACTER",
                Align = ControlAlign.HorizontalCenter,
                Y = (int)DeleteLayout.TitleY,
                FontSize = DeleteLayout.TitleFont,
                TextColor = new Color(220, 80, 80)
            });

            // Mensagem (multi-linha)
            Controls.Add(new LabelControl
            {
                Text = $"Are you sure you want to delete '{_characterName}'?\n\nThis action cannot be undone!\n\nEnter your security code to confirm:",
                Align = ControlAlign.HorizontalCenter,
                Y = (int)DeleteLayout.MessageY,
                FontSize = DeleteLayout.MessageFont,
                TextColor = new Color(240, 240, 245),
                AutoViewSize = false,
                ViewSize = new Point((int)DeleteLayout.MessageW, 90),
                TextAlign = HorizontalAlign.Center
            });

            // "Security Code:"
            Controls.Add(new LabelControl
            {
                Text = "Security Code:",
                X = (int)DeleteLayout.CodeLabelX,
                Y = (int)DeleteLayout.CodeLabelY,
                FontSize = DeleteLayout.CodeLabelFont,
                TextColor = new Color(240, 240, 245)
            });

            // Input do código (estilo Flat, igual login)
            _codeInput = TextFieldControl.Create();
            _codeInput.X = (int)DeleteLayout.CodeInputX;
            _codeInput.Y = (int)DeleteLayout.CodeInputY;
            _codeInput.AutoViewSize = false;
            _codeInput.ViewSize = new Point((int)DeleteLayout.CodeInputW, (int)DeleteLayout.CodeInputH);
            _codeInput.Skin = TextFieldSkin.Flat;
            _codeInput.FlatBackgroundColor = new Color(12, 12, 16, 235);
            _codeInput.FlatBorderColor = new Color(90, 78, 46, 255);
            _codeInput.FlatBorderThickness = 1;
            _codeInput.FontSize = DeleteLayout.CodeInputFont;
            Controls.Add(_codeInput);
            _codeInput.Click += (s, e) => _codeInput.OnFocus();

            // OK / Cancel (mesma arte da tela de login)
            _okButton = new OkCancelButton
            {
                Text = "OK",
                X = (int)DeleteLayout.OkX,
                Y = (int)DeleteLayout.OkY,
                ViewSize = new Point((int)DeleteLayout.OkW, (int)DeleteLayout.OkH),
                FontScale = DeleteLayout.OkFont / 24f
            };
            _okButton.Click += (s, e) => DeleteConfirmed?.Invoke(this, _codeInput.Value?.Trim() ?? string.Empty);
            Controls.Add(_okButton);

            _cancelButton = new OkCancelButton
            {
                Text = "Cancel",
                IsCancel = true,
                X = (int)DeleteLayout.CancelX,
                Y = (int)DeleteLayout.CancelY,
                ViewSize = new Point((int)DeleteLayout.CancelW, (int)DeleteLayout.CancelH),
                FontScale = DeleteLayout.CancelFont / 24f
            };
            _cancelButton.Click += (s, e) => CancelRequested?.Invoke(this, EventArgs.Empty);
            Controls.Add(_cancelButton);

            BringToFront();
        }
    }
}
