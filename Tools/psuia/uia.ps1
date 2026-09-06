param(
  [Parameter(Mandatory=$true)][string]$cmd,
  [int]$x, [int]$y, [int]$x2, [int]$y2,
  [string]$text, [string]$out = "$PSScriptRoot\shot.png"
)
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class U {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int X, int Y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint cButtons, UIntPtr dwExtraInfo);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
  [DllImport("user32.dll")] public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
  public const uint LEFTDOWN=0x02, LEFTUP=0x04, RIGHTDOWN=0x08, RIGHTUP=0x10;
  public static void Click(int x, int y){
    SetCursorPos(x,y); System.Threading.Thread.Sleep(60);
    mouse_event(LEFTDOWN,0,0,0,UIntPtr.Zero); System.Threading.Thread.Sleep(40);
    mouse_event(LEFTUP,0,0,0,UIntPtr.Zero);
  }
  public static void Drag(int x1,int y1,int x2,int y2){
    SetCursorPos(x1,y1); System.Threading.Thread.Sleep(120);
    mouse_event(LEFTDOWN,0,0,0,UIntPtr.Zero); System.Threading.Thread.Sleep(150);
    int steps=25;
    for(int i=1;i<=steps;i++){ SetCursorPos(x1+(x2-x1)*i/steps, y1+(y2-y1)*i/steps); System.Threading.Thread.Sleep(20);}
    System.Threading.Thread.Sleep(120);
    mouse_event(LEFTUP,0,0,0,UIntPtr.Zero);
  }
}
"@
switch ($cmd) {
  "shot" {
    $b = [System.Windows.Forms.SystemInformation]::VirtualScreen
    $bmp = New-Object System.Drawing.Bitmap $b.Width, $b.Height
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($b.X, $b.Y, 0, 0, $bmp.Size)
    $bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
    Write-Output "saved $out ($($b.Width)x$($b.Height) at $($b.X),$($b.Y))"
  }
  "click" { [U]::Click($x, $y); Write-Output "clicked $x,$y" }
  "rclick" { [U]::SetCursorPos($x,$y); Start-Sleep -m 60; [U]::mouse_event([U]::RIGHTDOWN,0,0,0,[UIntPtr]::Zero); Start-Sleep -m 40; [U]::mouse_event([U]::RIGHTUP,0,0,0,[UIntPtr]::Zero); Write-Output "rightclicked $x,$y" }
  "drag" { [U]::Drag($x, $y, $x2, $y2); Write-Output "dragged $x,$y -> $x2,$y2" }
  "focus" {
    $p = Get-Process -Id $x -ErrorAction Stop
    [U]::ShowWindow($p.MainWindowHandle, 9) | Out-Null
    [U]::SetForegroundWindow($p.MainWindowHandle) | Out-Null
    Write-Output "focused pid $x"
  }
  "keys" {
    [System.Windows.Forms.SendKeys]::SendWait($text)
    Write-Output "sent keys: $text"
  }
}
