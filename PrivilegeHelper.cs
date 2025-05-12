using System.ComponentModel;
using System.Runtime.InteropServices;

namespace BetterTeams
{
    public static class PrivilegeHelper
    {
        private const string SE_TAKE_OWNERSHIP_NAME = "SeTakeOwnershipPrivilege";
        private const string SE_SECURITY_NAME = "SeSecurityPrivilege";

        [DllImport("advapi32.dll", SetLastError = true)]
        static extern bool OpenProcessToken(IntPtr ProcessHandle,
            uint DesiredAccess, out IntPtr TokenHandle);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern bool LookupPrivilegeValue(string lpSystemName,
            string lpName, out LUID lpLuid);

        [DllImport("advapi32.dll", SetLastError = true)]
        static extern bool AdjustTokenPrivileges(IntPtr TokenHandle,
            bool DisableAllPrivileges, ref TOKEN_PRIVILEGES NewState,
            uint BufferLength, IntPtr PreviousState, IntPtr ReturnLength);

        [StructLayout(LayoutKind.Sequential)]
        struct TOKEN_PRIVILEGES
        {
            public uint PrivilegeCount;
            public LUID Luid;
            public uint Attributes;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct LUID
        {
            public uint LowPart;
            public int HighPart;
        }

        const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
        const uint TOKEN_QUERY = 0x0008;
        const uint SE_PRIVILEGE_ENABLED = 0x00000002;

        /// <summary>
        /// Enables the given privilege for the current process token.
        /// </summary>
        public static void EnablePrivilege(string privilege)
        {
            if (!OpenProcessToken(
                System.Diagnostics.Process.GetCurrentProcess().Handle,
                TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY,
                out var tokenHandle))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            if (!LookupPrivilegeValue(null, privilege, out var luid))
                throw new Win32Exception(Marshal.GetLastWin32Error());

            var tp = new TOKEN_PRIVILEGES
            {
                PrivilegeCount = 1,
                Luid = luid,
                Attributes = SE_PRIVILEGE_ENABLED
            };

            if (!AdjustTokenPrivileges(tokenHandle, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        /// <summary>
        /// Call this before any SetOwner or SetAccessControl that fails with UnauthorizedAccess.
        /// </summary>
        public static void EnableTakeOwnership()
        {
            // Must also enable SECURITY if you later modify SACLs
            EnablePrivilege(SE_TAKE_OWNERSHIP_NAME);
            EnablePrivilege(SE_SECURITY_NAME);
        }
    }
}
