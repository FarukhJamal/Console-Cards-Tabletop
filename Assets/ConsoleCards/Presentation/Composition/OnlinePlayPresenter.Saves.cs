using System;
using ConsoleCards.Application.Authority;

namespace ConsoleCards.Presentation.Composition
{
    /// <summary>
    /// Doc 58 step 4: a saved table is the same packet a host sends to a joining computer (the game's set-up and the
    /// table at that moment), so loading one goes through the same path: the game is dealt again with the saved players,
    /// then the saved moment is put on it.
    /// </summary>
    internal sealed partial class OnlinePlayPresenter
    {
        /// <summary>The whole table, packed as for sending (null when there is no table or it could not be packed).</summary>
        // A save starts with "CCS1" and Trap Floor's stage (the packet itself has no stage), then the packet.
        private static readonly byte[] SaveMagic = { (byte)'C', (byte)'C', (byte)'S', (byte)'1' };

        internal byte[] PackTableForSave()
        {
            if (!sharedTable) lastFloorIndex = host.trapFloorNextFloorIndex; // offline the floor is not tracked for sending
            byte[] payload = TablePayload();
            if (payload == null) return null;
            byte[] data = new byte[SaveMagic.Length + 4 + payload.Length];
            Buffer.BlockCopy(SaveMagic, 0, data, 0, SaveMagic.Length);
            Buffer.BlockCopy(BitConverter.GetBytes(host.trapFloorStageIndex), 0, data, SaveMagic.Length, 4);
            Buffer.BlockCopy(payload, 0, data, SaveMagic.Length + 4, payload.Length);
            return data;
        }

        /// <summary>Puts a saved table on this computer (not while sharing a table online). False with the reason.</summary>
        internal bool LoadSavedTable(byte[] data, out string error)
        {
            error = null;
            if (sharedTable)
            {
                error = "leave the online room first";
                return false;
            }

            OnlineTablePacket packet;
            int offset = 0;
            int stage = -1;
            if (data != null && data.Length > SaveMagic.Length + 4 && data[0] == SaveMagic[0] && data[1] == SaveMagic[1]
                && data[2] == SaveMagic[2] && data[3] == SaveMagic[3])
            {
                stage = BitConverter.ToInt32(data, SaveMagic.Length);
                offset = SaveMagic.Length + 4;
            }

            try
            {
                packet = data != null && data.Length > offset ? TableWire.Read(data, offset, data.Length - offset, true) as OnlineTablePacket : null;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }

            if (packet == null || packet.Setup == null || packet.Snapshot == null)
            {
                error = "the file is not a saved table";
                return false;
            }

            if (stage >= 0)
            {
                // The saved stage is dealt (a first floor takes the next stage; a later floor keeps the stage in play).
                host.trapFloorNextStageIndex = stage;
                host.trapFloorStageIndex = stage;
            }

            if (ApplyPacket(packet)) return true;
            error = "it did not fit this version of the game (the log has the details)";
            return false;
        }
    }
}
