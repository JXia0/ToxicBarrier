Add-Type -AssemblyName System.Drawing
$results = foreach ($name in @('tower0','tower1','tower2','tower3')) {
 $p = Join-Path $PSScriptRoot "$name.png"
 $b = [System.Drawing.Bitmap]::new($p)
 $rect = [System.Drawing.Rectangle]::new(0,0,$b.Width,$b.Height)
 $bits = $b.LockBits($rect,[System.Drawing.Imaging.ImageLockMode]::ReadOnly,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
 $data = New-Object byte[] ([Math]::Abs($bits.Stride)*$b.Height)
 [System.Runtime.InteropServices.Marshal]::Copy($bits.Scan0,$data,0,$data.Length)
 $b.UnlockBits($bits)
 $zero=0; $partial=0; $opaque=0; $minX=$b.Width; $minY=$b.Height; $maxX=-1; $maxY=-1; $border=0
 $hist=New-Object int[] 256; $vx=$b.Width; $vy=$b.Height; $vmaxX=-1; $vmaxY=-1
 for($y=0;$y -lt $b.Height;$y++){for($x=0;$x -lt $b.Width;$x++){
  $a=$data[$y*[Math]::Abs($bits.Stride)+$x*4+3]
  $hist[$a]++
  if($a -ge 128){$vx=[Math]::Min($vx,$x);$vy=[Math]::Min($vy,$y);$vmaxX=[Math]::Max($vmaxX,$x);$vmaxY=[Math]::Max($vmaxY,$y)}
  if($a -eq 0){$zero++}else{
   if($a -eq 255){$opaque++}else{$partial++}
   $minX=[Math]::Min($minX,$x);$minY=[Math]::Min($minY,$y);$maxX=[Math]::Max($maxX,$x);$maxY=[Math]::Max($maxY,$y)
   if($x -eq 0 -or $y -eq 0 -or $x -eq ($b.Width-1) -or $y -eq ($b.Height-1)){$border++}
  }
 }}
 [pscustomobject]@{name=$name;width=$b.Width;height=$b.Height;pixelFormat=$b.PixelFormat.ToString();transparent=$zero;partial=$partial;opaque=$opaque;transparentPercent=[Math]::Round(100*$zero/($b.Width*$b.Height),2);bounds=@($minX,$minY,($maxX+1),($maxY+1));visibleBounds=@($vx,$vy,($vmaxX+1),($vmaxY+1));histogram=$hist;nonTransparentBorderPixels=$border;sha256=(Get-FileHash -Algorithm SHA256 -LiteralPath $p).Hash}
 $b.Dispose()
}
$results | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'alpha-report.json') -Encoding utf8
$results | ConvertTo-Json -Depth 5

