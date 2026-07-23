using Client.Main.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;

namespace Client.Main.Controls.UI.Login
{
    public class LoginDialog : PopupFieldDialog
    {
        // Fields
        private readonly TextureControl _line1;
        private readonly TextureControl _line2;
        private readonly TextFieldControl _userInput;
        private readonly LabelControl _serverNameLabel;
        private readonly TextFieldControl _passwordInput;
        private readonly OkCancelButton _okButton;
        private readonly OkCancelButton _cancelButton;
        private readonly GoogleLoginButton _googleButton;

        // Properties
        public string ServerName
        {
            get => _serverNameLabel.Text;
            set => _serverNameLabel.Text = value;
        }

        /// <summary>
        /// Gets the username entered in the text field.
        /// </summary>
        public string Username => _userInput.Value;

        /// <summary>
        /// Gets the password entered in the text field.
        /// </summary>
        public string Password => _passwordInput.Value;

        // Events
        /// <summary>
        /// Invoked when the user confirms login (clicks OK or presses Enter in the password field).
        /// </summary>
        public event EventHandler LoginAttempt;

        /// <summary>
        /// Invoked when the user clicks Cancel — volta para a tela de seleção de servidor.
        /// </summary>
        public event EventHandler CancelAttempt;

        // Constructors
        public LoginDialog()
        {
            // Todo o layout vem de LoginLayout (editável pelo hud-edit-login).
            var L = typeof(LoginLayout); // referência p/ deixar claro de onde vêm os valores
            _ = L;

            ControlSize = new Point(LoginLayout.PanelW, LoginLayout.PanelH);

            Controls.Add(new LabelControl
            {
                Text = "MU Online",
                Align = ControlAlign.HorizontalCenter,
                Y = (int)LoginLayout.TitleY,
                FontSize = LoginLayout.TitleFont
            });

            Controls.Add(_line1 = new TextureControl
            {
                TexturePath = "Interface/GFx/popup_line_m.ozd",
                X = (int)LoginLayout.Line1X,
                Y = (int)LoginLayout.Line1Y,
                AutoViewSize = false
            });

            Controls.Add(_serverNameLabel = new LabelControl
            {
                Text = "OpenMU Server 1",
                Align = ControlAlign.HorizontalCenter,
                Y = (int)LoginLayout.ServerY,
                FontSize = LoginLayout.ServerFont,
                TextColor = new Color(241, 188, 37)
            });

            Controls.Add(new LabelControl
            {
                Text = "User",
                Y = (int)LoginLayout.UserLabelY,
                X = (int)LoginLayout.UserLabelX,
                AutoViewSize = false,
                ViewSize = new Point((int)LoginLayout.UserLabelW, 20),
                TextAlign = HorizontalAlign.Right,
                FontSize = LoginLayout.UserLabelFont
            });

            Controls.Add(new LabelControl
            {
                Text = "Password",
                Y = (int)LoginLayout.PassLabelY,
                X = (int)LoginLayout.PassLabelX,
                AutoViewSize = false,
                ViewSize = new Point((int)LoginLayout.PassLabelW, 20),
                TextAlign = HorizontalAlign.Right,
                FontSize = LoginLayout.PassLabelFont
            });

            Controls.Add(_line2 = new TextureControl
            {
                TexturePath = "Interface/GFx/popup_line_m.ozd",
                X = (int)LoginLayout.Line2X,
                Y = (int)LoginLayout.Line2Y,
                AutoViewSize = false,
                Alpha = 0.7f
            });

            // Inputs no estilo RealMU: retângulo LISO e reto (skin Flat).
            _userInput = TextFieldControl.Create();
            _userInput.X = (int)LoginLayout.UserInputX;
            _userInput.Y = (int)LoginLayout.UserInputY;
            _userInput.AutoViewSize = false;
            _userInput.ViewSize = new Point((int)LoginLayout.UserInputW, (int)LoginLayout.UserInputH);
            StyleInput(_userInput);
            _userInput.FontSize = LoginLayout.UserInputFont;
            _userInput.Value = "test1"; // pré-preenchido para teste rápido

            _passwordInput = TextFieldControl.Create();
            _passwordInput.X = (int)LoginLayout.PassInputX;
            _passwordInput.Y = (int)LoginLayout.PassInputY;
            _passwordInput.AutoViewSize = false;
            _passwordInput.ViewSize = new Point((int)LoginLayout.PassInputW, (int)LoginLayout.PassInputH);
            _passwordInput.MaskValue = true;
            StyleInput(_passwordInput);
            _passwordInput.FontSize = LoginLayout.PassInputFont;
            _passwordInput.Value = "test1"; // pré-preenchido para teste rápido

            _passwordInput.ValueChanged += PasswordInput_EnterPressed; // Use dedicated method
            Controls.Add(_userInput);
            Controls.Add(_passwordInput);

            _userInput.Click += (s, e) => { _userInput.OnFocus(); _passwordInput.OnBlur(); };
            _passwordInput.Click += (s, e) => { _passwordInput.OnFocus(); _userInput.OnBlur(); };

            // OK e Cancel: MESMA arte da tela de criação de char (ok_cancel.OZT).
            _okButton = new OkCancelButton
            {
                Text = "OK",
                X = (int)LoginLayout.OkX,
                Y = (int)LoginLayout.OkY,
                ViewSize = new Point((int)LoginLayout.OkW, (int)LoginLayout.OkH),
                FontScale = LoginLayout.OkFont / 24f
            };
            _okButton.Click += OkButton_Click;
            Controls.Add(_okButton);

            _cancelButton = new OkCancelButton
            {
                Text = "Cancel",
                IsCancel = true,
                X = (int)LoginLayout.CancelX,
                Y = (int)LoginLayout.CancelY,
                ViewSize = new Point((int)LoginLayout.CancelW, (int)LoginLayout.CancelH),
                FontScale = LoginLayout.CancelFont / 24f
            };
            _cancelButton.Click += CancelButton_Click;
            Controls.Add(_cancelButton);

            // Botão "Login with Google" — arte pronta (normal/hover).
            _googleButton = new GoogleLoginButton
            {
                X = (int)LoginLayout.GoogleX,
                Y = (int)LoginLayout.GoogleY,
                ViewSize = new Point((int)LoginLayout.GoogleW, (int)LoginLayout.GoogleH)
            };
            _googleButton.Click += GoogleButton_Click;
            Controls.Add(_googleButton);
        }

        /// <summary>Login via Google (por enquanto só dispara o evento pra cena tratar).</summary>
        public event EventHandler GoogleLoginAttempt;
        private void GoogleButton_Click(object sender, EventArgs e)
            => GoogleLoginAttempt?.Invoke(this, EventArgs.Empty);

        // Public Methods
        /// <summary>
        /// Sets focus on the username field (called from the scene).
        /// </summary>
        public void FocusUsername()
        {
            MuGame.ScheduleOnMainThread(() => // Ensure it's on the main thread
            {
                _userInput?.OnFocus();
                _passwordInput?.OnBlur();
            });
        }

        public override void Update(GameTime gameTime)
        {
            // Handle Tab key to switch focus between input fields
            if (MuGame.Instance.Keyboard.IsKeyDown(Keys.Tab) && MuGame.Instance.PrevKeyboard.IsKeyUp(Keys.Tab))
            {
                if (_userInput.IsFocused)
                {
                    _userInput.OnBlur();
                    _passwordInput.OnFocus();
                }
                else if (_passwordInput.IsFocused)
                {
                    _passwordInput.OnBlur();
                    _userInput.OnFocus();
                }
            }
            base.Update(gameTime);
        }

        // Protected Methods
        protected override void OnScreenSizeChanged()
        {
            _line1.ViewSize = new Point(DisplaySize.X - (int)(LoginLayout.Line1X * 2), (int)LoginLayout.Line1H);
            _line2.ViewSize = new Point(DisplaySize.X - (int)(LoginLayout.Line2X * 2), (int)LoginLayout.Line2H);
            base.OnScreenSizeChanged();
        }

        // Private Methods

        /// <summary>Estilo RealMU do input: retângulo liso, fundo escuro sólido + borda fina.</summary>
        private static void StyleInput(TextFieldControl input)
        {
            input.Skin = TextFieldSkin.Flat;
            // Flat* (não BackgroundColor/BorderColor do base): o fundo é desenhado ANTES do
            // texto pelo próprio TextFieldControl. Usar os do base fazia o base.Draw
            // redesenhar o fundo POR CIMA do texto.
            input.FlatBackgroundColor = new Color(12, 12, 16, 235);  // preto quase sólido
            input.FlatBorderColor = new Color(90, 78, 46, 255);      // borda dourada discreta
            input.FlatBorderThickness = 1;
            input.FontSize = 11f;                                    // menor => folga no campo
        }

        // Method called after clicking the OK button
        private void OkButton_Click(object sender, EventArgs e)
        {
            AttemptLogin();
        }

        // Cancel: fecha o teclado e volta pra seleção de servidor.
        private void CancelButton_Click(object sender, EventArgs e)
        {
            _userInput.OnBlur();
            _passwordInput.OnBlur();
            if (Scene != null && (Scene.FocusControl == _userInput || Scene.FocusControl == _passwordInput))
            {
                Scene.FocusControl = null;
            }
            CancelAttempt?.Invoke(this, EventArgs.Empty);
        }

        // Method called after pressing Enter in the password field
        private void PasswordInput_EnterPressed(object sender, EventArgs e)
        {
            // ValueChanged is also invoked on text change,
            // so we check if Enter was just pressed.
            bool enterPressed = MuGame.Instance.Keyboard.IsKeyDown(Keys.Enter) &&
                                MuGame.Instance.PrevKeyboard.IsKeyUp(Keys.Enter);

            if (enterPressed)
            {
                AttemptLogin();
            }
        }

        // Invokes the LoginAttempt event
        private void AttemptLogin()
        {
            // Blur fields to hide soft keyboard (especially on mobile) after submitting.
            _userInput.OnBlur();
            _passwordInput.OnBlur();
            if (Scene != null && (Scene.FocusControl == _userInput || Scene.FocusControl == _passwordInput))
            {
                Scene.FocusControl = null; // keep focus cleared so keyboard stays hidden
            }

            Console.WriteLine("LoginDialog: Login attempt triggered."); // Debug log
            LoginAttempt?.Invoke(this, EventArgs.Empty);
        }
    }
}
