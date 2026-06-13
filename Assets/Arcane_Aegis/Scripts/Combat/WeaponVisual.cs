using UnityEngine;
using ArcaneShared.Enums;
using Arcane_Aegis.Content;
using Arcane_Aegis.Entities;
using Arcane_Aegis.Items;

namespace Arcane_Aegis.Combat
{
    /// <summary>
    /// Shows a player's equipped main-hand weapon as a 3D model, attached to a SOCKET on the character rig — in the
    /// hand while in combat, on the back out of combat. Sockets ("Socket_MainHand" / "Socket_Back") are placed by the
    /// artist on each character model (so positioning is WYSIWYG and per-skeleton); the weapon snaps with zero offset
    /// (the item's grip/sheath AttachPoint is an optional nudge). Works for ALL players: the LOCAL one reads the
    /// equipped item from InventoryStore; REMOTES read the replicated id (EntityView.EquippedMainHand). Cosmetic only.
    /// </summary>
    public sealed class WeaponVisual : MonoBehaviour
    {
        private const string DefaultGripSocket = "Socket_MainHand";
        private const string DefaultSheathSocket = "Socket_Back";

        private EntityView _view;
        private PlayerView _pv;
        private ItemDefinitionSO _so;
        private GameObject _weapon;
        private string _currentTemplateId = "\0"; // sentinel so the first real value (incl. "") rebuilds
        private Transform _gripSocket, _sheathSocket;
        private bool _socketsResolved;
        private bool _sheathed;

        private void Awake()
        {
            _view = GetComponent<EntityView>();
            _pv = _view as PlayerView;
        }

        private void Update()
        {
            string id = CurrentEquippedId();
            if (id != _currentTemplateId) Rebuild(id);
            if (_weapon == null) return;

            bool wantSheath = _sheathSocket != null && !InCombat();
            if (wantSheath != _sheathed) { _sheathed = wantSheath; Attach(); }
        }

        private void Rebuild(string templateId)
        {
            _currentTemplateId = templateId;
            if (_weapon != null) { Destroy(_weapon); _weapon = null; }

            _so = (!string.IsNullOrEmpty(templateId) && ContentLibrary.Active != null) ? ContentLibrary.Active.GetItem(templateId) : null;
            if (_so == null || _so.model3D == null) return;

            ResolveSockets();
            _weapon = Instantiate(_so.model3D);
            foreach (var col in _weapon.GetComponentsInChildren<Collider>()) Destroy(col); // visual only
            _sheathed = _sheathSocket != null && !InCombat();
            Attach();
        }

        private void Attach()
        {
            if (_weapon == null || _so == null) return;
            bool useSheath = _sheathed && _sheathSocket != null;
            Transform socket = useSheath ? _sheathSocket : (_gripSocket ?? _sheathSocket ?? transform);
            ItemDefinitionSO.AttachPoint nudge = useSheath ? _so.sheathAttach : _so.gripAttach;

            _weapon.transform.SetParent(socket, false);
            _weapon.transform.localPosition = nudge.position;
            _weapon.transform.localRotation = Quaternion.Euler(nudge.euler);
            _weapon.transform.localScale = nudge.scale == Vector3.zero ? Vector3.one : nudge.scale;
        }

        private void ResolveSockets()
        {
            if (_socketsResolved) return;
            _socketsResolved = true;
            string grip = _so != null && !string.IsNullOrEmpty(_so.gripAttach.bone) ? _so.gripAttach.bone : DefaultGripSocket;
            string sheath = _so != null && !string.IsNullOrEmpty(_so.sheathAttach.bone) ? _so.sheathAttach.bone : DefaultSheathSocket;
            _gripSocket = FindDeep(transform, grip);
            _sheathSocket = FindDeep(transform, sheath);
        }

        private bool InCombat() => _view != null && CombatStance.InCombat(_view.Id);

        /// <summary>LOCAL player → from the inventory; REMOTE → from the replicated spawn/equip field.</summary>
        private string CurrentEquippedId()
        {
            if (_pv != null && _pv.IsLocal)
            {
                var store = InventoryStore.Instance;
                if (store == null) return "";
                foreach (var it in store.Items)
                    if (it.Container == ItemContainer.Equipped && (EquipSlot)it.Slot == EquipSlot.MainHand) return it.TemplateId;
                return "";
            }
            return _view != null ? (_view.EquippedMainHand ?? "") : "";
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (parent.name == name) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform f = FindDeep(parent.GetChild(i), name);
                if (f != null) return f;
            }
            return null;
        }
    }
}
