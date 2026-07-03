using System.Windows;
using System.Windows.Input;
using BackupDbServer.Helpers;

namespace BackupDbServer.Views
{
    /// <summary>
    /// وضع نافذة كلمة السر.
    /// </summary>
    public enum PasswordPromptMode
    {
        /// <summary>التحقق من كلمة السر لفتح التطبيق.</summary>
        Unlock,
        /// <summary>تعيين كلمة سر جديدة (حقل كلمة السر + تأكيد).</summary>
        Setup,
        /// <summary>تغيير كلمة السر (التحقق من القديمة ثم تعيين الجديدة).</summary>
        Change
    }

    /// <summary>
    /// نافذة إدخال/تعيين كلمة سر التطبيق.
    /// </summary>
    public partial class PasswordPromptWindow : Window
    {
        private readonly AppLockManager _lock;
        private readonly PasswordPromptMode _mode;

        // وضع التغيير: التحقق من القديمة أولاً ثم تعيين الجديدة
        private bool _oldVerified;

        public PasswordPromptWindow(AppLockManager appLock, PasswordPromptMode mode)
        {
            InitializeComponent();
            _lock = appLock;
            _mode = mode;
            ConfigureForMode();
            Loaded += (_, _) =>
            {
                // ضمان ظهور النافذة في المقدمة وأخذ التركيز (مهمة عند بدء التشغيل)
                Activate();
                Topmost = true;
                Topmost = false;
                PasswordInput.Focus();
            };
        }

        private void ConfigureForMode()
        {
            switch (_mode)
            {
                case PasswordPromptMode.Unlock:
                    HeaderText.Text = "إدخال كلمة السر";
                    DescriptionText.Text = "البرنامج مقفل. أدخل كلمة السر لإظهار النافذة.";
                    ConfirmInput.Visibility = Visibility.Collapsed;
                    break;

                case PasswordPromptMode.Setup:
                    HeaderText.Text = "تعيين كلمة السر";
                    DescriptionText.Text = "أدخل كلمة السر الجديدة ثم أعد إدخالها للتأكيد.";
                    ConfirmInput.Visibility = Visibility.Visible;
                    break;

                case PasswordPromptMode.Change:
                    HeaderText.Text = "تغيير كلمة السر";
                    DescriptionText.Text = "أدخل كلمة السر الحالية أولاً.";
                    ConfirmInput.Visibility = Visibility.Collapsed;
                    break;
            }
        }

        private void Input_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Submit();
            }
            else if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e) => Submit();

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Submit()
        {
            HideError();
            var password = PasswordInput.Password;

            switch (_mode)
            {
                case PasswordPromptMode.Unlock:
                    if (_lock.VerifyPassword(password))
                    {
                        DialogResult = true;
                        Close();
                    }
                    else
                    {
                        ShowError("كلمة السر غير صحيحة.");
                        PasswordInput.Clear();
                        PasswordInput.Focus();
                    }
                    break;

                case PasswordPromptMode.Setup:
                    if (string.IsNullOrEmpty(password))
                    {
                        ShowError("كلمة السر لا يمكن أن تكون فارغة.");
                        return;
                    }
                    if (password != ConfirmInput.Password)
                    {
                        ShowError("كلمتا السر غير متطابقتين.");
                        ConfirmInput.Clear();
                        ConfirmInput.Focus();
                        return;
                    }
                    _lock.SetPassword(password);
                    DialogResult = true;
                    Close();
                    break;

                case PasswordPromptMode.Change:
                    if (!_oldVerified)
                    {
                        if (!_lock.VerifyPassword(password))
                        {
                            ShowError("كلمة السر الحالية غير صحيحة.");
                            PasswordInput.Clear();
                            PasswordInput.Focus();
                            return;
                        }
                        // الانتقال إلى مرحلة إدخال الكلمة الجديدة
                        _oldVerified = true;
                        DescriptionText.Text = "أدخل كلمة السر الجديدة ثم أعد إدخالها للتأكيد.";
                        ConfirmInput.Visibility = Visibility.Visible;
                        PasswordInput.Clear();
                        ConfirmInput.Clear();
                        PasswordInput.Focus();
                        return;
                    }
                    // مرحلة الكلمة الجديدة
                    if (string.IsNullOrEmpty(password))
                    {
                        ShowError("كلمة السر لا يمكن أن تكون فارغة.");
                        return;
                    }
                    if (password != ConfirmInput.Password)
                    {
                        ShowError("كلمتا السر غير متطابقتين.");
                        ConfirmInput.Clear();
                        ConfirmInput.Focus();
                        return;
                    }
                    _lock.SetPassword(password);
                    DialogResult = true;
                    Close();
                    break;
            }
        }

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;
        }

        private void HideError()
        {
            ErrorText.Visibility = Visibility.Collapsed;
        }
    }
}
