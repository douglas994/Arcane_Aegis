using System;
using NetworkLibrary.Serialization;
using ArcaneShared.Enums;
using ArcaneShared.Models;
using ArcaneShared.Protocol.ServerToClient;

namespace Arcane_Aegis.Network.Handlers
{
    /// <summary>S2C_CreationData → the data-driven race/class options for the create screen.</summary>
    public sealed class CreationDataHandler : IClientPacketHandler
    {
        private readonly Action<CreationOption[], CreationOption[]> _onData;
        public CreationDataHandler(Action<CreationOption[], CreationOption[]> onData) => _onData = onData;
        public PacketId PacketId => PacketId.S2C_CreationData;
        public void Handle(ref BitBuffer reader)
        {
            var p = new S2C_CreationData();
            p.Deserialize(ref reader);
            _onData(p.Races ?? Array.Empty<CreationOption>(), p.Classes ?? Array.Empty<CreationOption>());
        }
    }

    /// <summary>S2C_CharacterList → this account's characters.</summary>
    public sealed class CharacterListHandler : IClientPacketHandler
    {
        private readonly Action<CharacterSummary[]> _onList;
        public CharacterListHandler(Action<CharacterSummary[]> onList) => _onList = onList;
        public PacketId PacketId => PacketId.S2C_CharacterList;
        public void Handle(ref BitBuffer reader)
        {
            var p = new S2C_CharacterList();
            p.Deserialize(ref reader);
            _onList(p.Characters ?? Array.Empty<CharacterSummary>());
        }
    }

    /// <summary>S2C_CharacterCreateResult → outcome of a create-character request.</summary>
    public sealed class CharacterCreateResultHandler : IClientPacketHandler
    {
        private readonly Action<CharCreateResult> _onResult;
        public CharacterCreateResultHandler(Action<CharCreateResult> onResult) => _onResult = onResult;
        public PacketId PacketId => PacketId.S2C_CharacterCreateResult;
        public void Handle(ref BitBuffer reader)
        {
            var p = new S2C_CharacterCreateResult();
            p.Deserialize(ref reader);
            _onResult((CharCreateResult)p.Result);
        }
    }
}
