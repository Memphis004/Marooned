$sig = '[DllImport("user32.dll")] public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);'
Add-Type -MemberDefinition $sig -Name Kbd -Namespace Win32
$shell = New-Object -ComObject WScript.Shell
$p = Get-Process Unity | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if ($p -eq $null) { Write-Output "NO UNITY WINDOW"; exit 1 }
$ok = $shell.AppActivate($p.Id)
Start-Sleep -Milliseconds 800
Write-Output "Focused=$ok proc=$($p.Id)"
[Win32.Kbd]::keybd_event(0x44, 0, 0, [UIntPtr]::Zero)   # D down
Start-Sleep -Milliseconds 2000
[Win32.Kbd]::keybd_event(0x44, 0, 2, [UIntPtr]::Zero)   # D up
Start-Sleep -Milliseconds 1200
[Win32.Kbd]::keybd_event(0x44, 0, 0, [UIntPtr]::Zero)   # D down again (finish crossing strip)
Start-Sleep -Milliseconds 700
[Win32.Kbd]::keybd_event(0x44, 0, 2, [UIntPtr]::Zero)   # D up
Write-Output "WALK DONE"
