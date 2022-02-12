//////////////////////////////////////////////////////////////////////////////////////////////
//
//  Pulled from Ottermandias's HudManager for RezPls (https://github.com/Ottermandias/RezPls).
//  Modified and condensed a bit because I'm bad and just want something simple to bolt on.
//
//////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Runtime.InteropServices;

using Dalamud.Hooking;
using Dalamud.Logging;
using Dalamud.Game;

namespace ReadyCheckHelper
{
    public class SeFunctionBase<T> where T : Delegate
    {
        public    IntPtr Address;
        protected T?     FuncDelegate;

        public SeFunctionBase( SigScanner sigScanner, int offset )
        {
            Address = sigScanner.Module.BaseAddress + offset;
            PluginLog.Debug( $"{GetType().Name} address 0x{Address.ToInt64():X16}, baseOffset 0x{offset:X16}." );
        }

        public SeFunctionBase( SigScanner sigScanner, string signature, int offset = 0 )
        {
            Address = sigScanner.ScanText( signature );
            if( Address != IntPtr.Zero )
                Address += offset;
            var baseOffset = (ulong) Address.ToInt64() - (ulong) sigScanner.Module.BaseAddress.ToInt64();
            PluginLog.Debug( $"{GetType().Name} address 0x{Address.ToInt64():X16}, baseOffset 0x{baseOffset:X16}." );
        }

        public T? Delegate()
        {
            if( FuncDelegate != null )
                return FuncDelegate;

            if( Address != IntPtr.Zero )
            {
                FuncDelegate = Marshal.GetDelegateForFunctionPointer<T>( Address );
                return FuncDelegate;
            }

            PluginLog.Error( $"Trying to generate delegate for {GetType().Name}, but no pointer available." );
            return null;
        }

        public dynamic? Invoke( params dynamic[] parameters )
        {
            if( FuncDelegate != null )
                return FuncDelegate.DynamicInvoke( parameters );

            if( Address != IntPtr.Zero )
            {
                FuncDelegate = Marshal.GetDelegateForFunctionPointer<T>( Address );
                return FuncDelegate!.DynamicInvoke( parameters );
            }
            else
            {
                PluginLog.Error( $"Trying to call {GetType().Name}, but no pointer available." );
                return null;
            }
        }

        public Hook<T>? CreateHook( T detour )
        {
            if( Address != IntPtr.Zero )
            {
                var hook = new Hook<T>(Address, detour);
                hook.Enable();
                PluginLog.Debug( $"Hooked onto {GetType().Name} at address 0x{Address.ToInt64():X16}." );
                return hook;
            }

            PluginLog.Error( $"Trying to create Hook for {GetType().Name}, but no pointer available." );
            return null;
        }
    }

    public delegate void UpdatePartyDelegate( IntPtr hudAgent );

    public sealed class UpdateParty : SeFunctionBase<UpdatePartyDelegate>
    {
        public UpdateParty( SigScanner sigScanner )
            : base( sigScanner, "40 ?? 48 83 ?? ?? 48 8B ?? 48 ?? ?? ?? 48 ?? ?? ?? ?? ?? ?? 83 ?? ?? ?? ?? ?? ?? 74 ?? 48" )
        { }
    }

    public unsafe class HudManager : IDisposable
    {
        private const int GroupMemberOffset    = 0x0BE0;
        private const int AllianceMemberOffset = 0x0D24;
        private const int AllianceSizeOffset   = 0x0DC4;
        private const int GroupMemberSize      = 0x20;
        private const int GroupMemberIdOffset  = 0x10;

        private readonly Hook<UpdatePartyDelegate> _updatePartyHook;
        private          IntPtr                    _hudAgentPtr = IntPtr.Zero;

        public HudManager( SigScanner sigScanner )
        {
            UpdateParty updateParty = new( sigScanner );
            _updatePartyHook = updateParty.CreateHook( UpdatePartyHook )!;
        }

        private readonly int[,] _idOffsets =
        {
            {
                GroupMemberOffset + 0 * GroupMemberSize + GroupMemberIdOffset,
                GroupMemberOffset + 1 * GroupMemberSize + GroupMemberIdOffset,
                GroupMemberOffset + 2 * GroupMemberSize + GroupMemberIdOffset,
                GroupMemberOffset + 3 * GroupMemberSize + GroupMemberIdOffset,
                GroupMemberOffset + 4 * GroupMemberSize + GroupMemberIdOffset,
                GroupMemberOffset + 5 * GroupMemberSize + GroupMemberIdOffset,
                GroupMemberOffset + 6 * GroupMemberSize + GroupMemberIdOffset,
                GroupMemberOffset + 7 * GroupMemberSize + GroupMemberIdOffset,

                AllianceMemberOffset + 0 * 4,
                AllianceMemberOffset + 1 * 4,
                AllianceMemberOffset + 2 * 4,
                AllianceMemberOffset + 3 * 4,
                AllianceMemberOffset + 4 * 4,
                AllianceMemberOffset + 5 * 4,
                AllianceMemberOffset + 6 * 4,
                AllianceMemberOffset + 7 * 4,
                AllianceMemberOffset + 8 * 4,
                AllianceMemberOffset + 9 * 4,
                AllianceMemberOffset + 10 * 4,
                AllianceMemberOffset + 11 * 4,
                AllianceMemberOffset + 12 * 4,
                AllianceMemberOffset + 13 * 4,
                AllianceMemberOffset + 14 * 4,
                AllianceMemberOffset + 15 * 4,
            },
            {
                GroupMemberOffset + 0 * GroupMemberSize + GroupMemberIdOffset,
                GroupMemberOffset + 1 * GroupMemberSize + GroupMemberIdOffset,
                GroupMemberOffset + 2 * GroupMemberSize + GroupMemberIdOffset,
                GroupMemberOffset + 3 * GroupMemberSize + GroupMemberIdOffset,

                AllianceMemberOffset + 0 * 4,
                AllianceMemberOffset + 1 * 4,
                AllianceMemberOffset + 2 * 4,
                AllianceMemberOffset + 3 * 4,
                AllianceMemberOffset + 8 * 4,
                AllianceMemberOffset + 9 * 4,
                AllianceMemberOffset + 10 * 4,
                AllianceMemberOffset + 11 * 4,
                AllianceMemberOffset + 16 * 4,
                AllianceMemberOffset + 17 * 4,
                AllianceMemberOffset + 18 * 4,
                AllianceMemberOffset + 19 * 4,
                AllianceMemberOffset + 24 * 4,
                AllianceMemberOffset + 25 * 4,
                AllianceMemberOffset + 26 * 4,
                AllianceMemberOffset + 27 * 4,
                AllianceMemberOffset + 32 * 4,
                AllianceMemberOffset + 33 * 4,
                AllianceMemberOffset + 34 * 4,
                AllianceMemberOffset + 35 * 4,
            },
        };

        public int GroupSize
            => *(int*)(_hudAgentPtr + AllianceSizeOffset);

        public bool IsAlliance
            => GroupSize == 8;

        public bool IsPvP
            => GroupSize == 4;

        public bool IsGroup
            => GroupSize == 0;

        public (int groupIdx, int idx)? FindGroupMemberById( uint actorId )
        {
            if( _hudAgentPtr == IntPtr.Zero )
                return null;

            var groupSize = GroupSize;
            int numGroups;
            if( groupSize == 0 )
            {
                numGroups = 1;
                groupSize = 8;
            }
            else
            {
                numGroups = groupSize == 4 ? 6 : 3;
            }

            var count = numGroups * groupSize;
            var pvp   = groupSize == 4 ? 1 : 0;
            for( var i = 0; i < count; ++i )
            {
                var id = *(uint*)(_hudAgentPtr + _idOffsets[pvp, i]);
                if( id == actorId )
                    return (i / groupSize, i % groupSize);
            }

            return null;
        }

        private void UpdatePartyHook( IntPtr hudAgent )
        {
            _hudAgentPtr = hudAgent;
            PluginLog.LogVerbose( $"Obtained HUD agent at address 0x{_hudAgentPtr.ToInt64():X16}." );
            _updatePartyHook.Original( hudAgent );
            _updatePartyHook.Disable();
            _updatePartyHook.Dispose();
        }

        public void Dispose()
            => _updatePartyHook.Dispose();
    }
}
