// SPDX-FileCopyrightText: 2026 Corvax-Forge
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Audio;
using Robust.Shared.Serialization;

namespace Content.Shared.Access.Components;

public sealed partial class IdCardConsoleComponent // corvax goob edit - made partial
{
    [DataField]
    public SoundSpecifier? BulkAccessSuccessSound = new SoundPathSpecifier("/Audio/Machines/quickbeep.ogg", AudioParams.Default.WithVolume(-6));

    [DataField]
    public SoundSpecifier? BulkAccessFailureSound = new SoundPathSpecifier("/Audio/Machines/custom_deny.ogg", AudioParams.Default.WithVolume(-6));

    [Serializable, NetSerializable]
    public sealed class IdCardConsoleBulkAccessMessage : BoundUserInterfaceMessage
    {
        public readonly IdCardConsoleBulkAccessAction Action;

        public IdCardConsoleBulkAccessMessage(IdCardConsoleBulkAccessAction action)
        {
            Action = action;
        }
    }

    [Serializable, NetSerializable]
    public enum IdCardConsoleBulkAccessAction : byte
    {
        DemoteToPassenger,
        StandardAccess,
        Extended,
        Full,
    }
}
