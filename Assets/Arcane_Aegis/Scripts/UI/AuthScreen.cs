using UnityEngine;
using UnityEngine.Events;
using TMPro;
using ArcaneShared.Enums;
using Arcane_Aegis.Network;

namespace Arcane_Aegis.UI
{
    /// <summary>
    /// One controller for the whole auth screen: LOGIN, CREATE ACCOUNT, and switching/closing the panels.
    /// Talks to <see cref="MasterClient"/> (the network layer, kept separate). Wire the buttons' OnClick:
    ///   • "Entrar"        → <see cref="OnLoginClicked"/>
    ///   • "Criar conta"   → <see cref="OnRegisterClicked"/>
    ///   • go to register  → <see cref="ShowRegister"/>
    ///   • back to login   → <see cref="ShowLogin"/>
    /// </summary>
    public class AuthScreen : MonoBehaviour
    {
        [Header("Net")]
        [SerializeField] private MasterClient master;

        [Header("Panels")]
        [SerializeField] private GameObject loginPanel;
        [SerializeField] private GameObject registerPanel;

        [Header("Login fields")]
        [SerializeField] private TMP_InputField loginUsername;
        [SerializeField] private TMP_InputField loginPassword;
        [SerializeField] private TMP_Text loginStatus;

        [Header("Register fields")]
        [SerializeField] private TMP_InputField regUsername;
        [SerializeField] private TMP_InputField regEmail;
        [SerializeField] private TMP_InputField regPassword;
        [SerializeField] private TMP_Text regStatus;

        [Header("After login")]
        [SerializeField] private GameObject afterLoginPanel;      // shown on success (drag the Server-Select panel here)
        [SerializeField] private UnityEvent onLoggedIn;           // optional extra hooks

        private enum Pending { None, Login, Register }
        private Pending _pending;

        private void Awake()
        {
            if (master == null) master = FindAnyObjectByType<MasterClient>();
            ShowLogin();
        }

        private void OnEnable()  { if (master != null) master.OnAuthResult += HandleAuthResult; }
        private void OnDisable() { if (master != null) master.OnAuthResult -= HandleAuthResult; _pending = Pending.None; }

        // ── panel switching (wire to buttons) ──
        public void ShowLogin()    { Set(loginPanel, true);  Set(registerPanel, false); }
        public void ShowRegister() { Set(loginPanel, false); Set(registerPanel, true);  }

        // ── actions (wire to buttons) ──
        public void OnLoginClicked()
        {
            if (!Ready(loginStatus)) return;
            SetText(loginStatus, "Entrando…");
            _pending = Pending.Login;
            master.Login(Text(loginUsername), Text(loginPassword));
        }

        public void OnRegisterClicked()
        {
            if (!Ready(regStatus)) return;
            SetText(regStatus, "Criando conta…");
            _pending = Pending.Register;
            master.CreateAccount(Text(regUsername), Text(regEmail), Text(regPassword));
        }

        // ── result (one handler; routes to whichever form is pending) ──
        private void HandleAuthResult(bool ok, AuthReason reason)
        {
            Pending what = _pending;
            _pending = Pending.None;
            if (what == Pending.None) return;

            TMP_Text status = what == Pending.Login ? loginStatus : regStatus;
            if (!ok) { SetText(status, Message(reason)); return; }

            if (what == Pending.Register)
            {
                SetText(regStatus, "Conta criada! Agora é só entrar.");
                ShowLogin(); // back to the login panel after creating
            }
            else
            {
                SetText(loginStatus, $"Bem-vindo! (conta #{master.AccountId})");
                Set(loginPanel, false);
                Set(registerPanel, false);
                Set(afterLoginPanel, true); // e.g. the Server-Select panel (its OnEnable requests the list)
                onLoggedIn?.Invoke();
            }
        }

        private bool Ready(TMP_Text status)
        {
            if (master == null) { SetText(status, "Sem MasterClient na cena."); return false; }
            if (!master.Connected) { SetText(status, "Conectando ao servidor…"); return false; }
            return true;
        }

        private static string Text(TMP_InputField field) => field != null ? field.text : string.Empty;
        private static void Set(GameObject panel, bool on) { if (panel != null) panel.SetActive(on); }
        private static void SetText(TMP_Text label, string msg) { if (label != null) label.text = msg; }

        private static string Message(AuthReason reason) => reason switch
        {
            AuthReason.BadCredentials => "Usuário ou senha incorretos.",
            AuthReason.UsernameTaken  => "Esse nome de usuário já existe.",
            AuthReason.EmailTaken     => "Esse email já está cadastrado.",
            AuthReason.InvalidInput   => "Dados inválidos (usuário 3-20, senha ≥4, email válido).",
            AuthReason.Banned         => "Conta banida.",
            _                         => "Erro no servidor. Tente de novo.",
        };
    }
}
