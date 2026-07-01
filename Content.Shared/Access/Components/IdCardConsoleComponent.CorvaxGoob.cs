// SPDX-FileCopyrightText: 2026 Corvax-Forge
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Audio;
using Robust.Shared.Serialization;

namespace Content.Shared.Access.Components;

public sealed partial class IdCardConsoleComponent
{
    [DataField]
    public SoundSpecifier? BulkAccessSuccessSound = new SoundPathSpecifier("/Audio/Machines/quickbeep.ogg", AudioParams.Default.WithVolume(-6));

    [DataField]
    public SoundSpecifier? BulkAccessFailureSound = new SoundPathSpecifier("/Audio/Machines/custom_deny.ogg", AudioParams.Default.WithVolume(-6));

    /// <summary>
    /// UI message for a bulk access action in the ID card console.
    /// </summary>
    /// <remarks>
    /// The client sends this to tell the server which <see cref="IdCardConsoleBulkAccessAction"/> was selected.
    /// </remarks>
    [Serializable, NetSerializable]
    public sealed class IdCardConsoleBulkAccessMessage : BoundUserInterfaceMessage
    {
        public readonly IdCardConsoleBulkAccessAction Action;

        public IdCardConsoleBulkAccessMessage(IdCardConsoleBulkAccessAction action)
        {
            Action = action;
        }
    }

    /// <summary>
    /// Bulk access actions for the ID card console.
    /// </summary>
    /// <remarks>
    /// Used by the client and server to identify which bulk access operation was selected: StandardAccess, Extended, or Full.
    /// </remarks>
    [Serializable, NetSerializable]
    public enum IdCardConsoleBulkAccessAction : byte
    {
        StandardAccess,
        Extended,
        Full,
    }
}
