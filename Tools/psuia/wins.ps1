Add-Type @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public struct RECTW { public int L; public int T; public int R; public int B; }
public delegate bool EnumProc(IntPtr h, IntPtr l);
public static class WE2 {
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECTW r);
}
'@
$targetPid = $args[0]
$cb = [EnumProc]{ param($h, $l)
  $p = 0
  [WE2]::GetWindowThreadProcessId($h, [ref]$p) | Out-Null
  if ($p -eq $targetPid -and [WE2]::IsWindowVisible($h)) {
    $sb = New-Object System.Text.StringBuilder 256
    [WE2]::GetWindowText($h, $sb, 256) | Out-Null
    $r = New-Object RECTW
    [WE2]::GetWindowRect($h, [ref]$r) | Out-Null
    Write-Output ("WIN: [" + $sb.ToString() + "] " + $r.L + "," + $r.T + " " + ($r.R - $r.L) + "x" + ($r.B - $r.T))
  }
  return $true
}
[WE2]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null
Write-Output DONE
