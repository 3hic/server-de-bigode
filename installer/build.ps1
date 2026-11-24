# Compila o ServerDeBigode.exe (usa o csc.exe que ja vem no Windows; nao precisa instalar nada).
# Uso (PowerShell):  .\build.ps1
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.IO.Compression.FileSystem
$here  = Split-Path -Parent $MyInvocation.MyCommand.Path
$dist  = Join-Path $here 'dist'
$stage = Join-Path $dist 'stage'
New-Item -ItemType Directory -Force $dist | Out-Null
if (Test-Path $stage) { [IO.Directory]::Delete($stage, $true) }
Copy-Item (Join-Path $here 'payload') $stage -Recurse

# scripts do celular precisam de final de linha LF
Get-ChildItem $stage -Recurse -File | Where-Object { $_.Extension -in '.sh','.js','.json','.css','.txt','.svg' -or $_.Name -eq '.env.example' } | ForEach-Object {
  $t = [IO.File]::ReadAllText($_.FullName) -replace "`r`n", "`n"; [IO.File]::WriteAllText($_.FullName, $t)
}
$zip = Join-Path $dist 'payload.zip'
if (Test-Path $zip) { [IO.File]::Delete($zip) }
[IO.Compression.ZipFile]::CreateFromDirectory($stage, $zip)

# icone (desenhado aqui mesmo: servidor com degrade frio) -> PNG dentro de um .ico
$bmp = New-Object System.Drawing.Bitmap 256, 256
$g = [System.Drawing.Graphics]::FromImage($bmp); $g.SmoothingMode = 'AntiAlias'; $g.Clear([System.Drawing.Color]::Transparent)
$g.FillEllipse((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 6, 16, 26))), 8, 8, 240, 240)
$grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush (New-Object System.Drawing.Rectangle 0, 0, 256, 256), ([System.Drawing.Color]::FromArgb(255, 92, 200, 255)), ([System.Drawing.Color]::FromArgb(255, 122, 162, 255)), 45
$pen = New-Object System.Drawing.Pen $grad, 9; $g.DrawEllipse($pen, 8, 8, 240, 240)
$pen2 = New-Object System.Drawing.Pen $grad, 11; $pen2.StartCap = 'Round'; $pen2.EndCap = 'Round'
foreach ($y in 74, 130) {
  $p = New-Object System.Drawing.Drawing2D.GraphicsPath; $x = 66; $w = 124; $h = 40; $r = 12
  $p.AddArc($x, $y, $r*2, $r*2, 180, 90); $p.AddArc($x+$w-$r*2, $y, $r*2, $r*2, 270, 90); $p.AddArc($x+$w-$r*2, $y+$h-$r*2, $r*2, $r*2, 0, 90); $p.AddArc($x, $y+$h-$r*2, $r*2, $r*2, 90, 90); $p.CloseFigure(); $g.DrawPath($pen2, $p)
  $g.FillEllipse((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 92, 200, 255))), 86, $y + 14, 12, 12)
}
$g.DrawLine($pen2, 100, 204, 156, 204); $g.Dispose()
$ms = New-Object IO.MemoryStream; $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png); $png = $ms.ToArray()
$ico = New-Object IO.MemoryStream; $bw = New-Object IO.BinaryWriter $ico
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]1); $bw.Write([byte]0); $bw.Write([byte]0); $bw.Write([byte]0); $bw.Write([byte]0)
$bw.Write([uint16]1); $bw.Write([uint16]32); $bw.Write([uint32]$png.Length); $bw.Write([uint32]22); $bw.Write($png); $bw.Flush()
$icoPath = Join-Path $dist 'icone.ico'; [IO.File]::WriteAllBytes($icoPath, $ico.ToArray())

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$exe = Join-Path $dist 'ServerDeBigode.exe'
& $csc /nologo /target:winexe "/out:$exe" "/win32icon:$icoPath" "/resource:$zip,payload.zip" `
  /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll (Join-Path $here 'ServerDeBigode.cs')
if ($LASTEXITCODE -ne 0) { throw "Falha na compilacao" }
Copy-Item (Join-Path $here 'creditos.ini') (Join-Path $dist 'creditos.ini') -Force
[IO.Directory]::Delete($stage, $true)
"OK: $exe  ($([math]::Round((Get-Item $exe).Length/1KB)) KB)"
