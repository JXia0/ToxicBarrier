$dir=Join-Path (Get-Location) 'output/ArtOnly/R05'
$data=@{}
foreach($name in @('tower0','tower1','tower2','tower3')){$data[$name]='data:image/png;base64,'+[Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $dir "$name.png")))}
foreach($n in 1..3){$data["map$n"]='data:image/png;base64,'+[Convert]::ToBase64String([IO.File]::ReadAllBytes("C:/GameProjects/ToxicBarrier/Assets/Sprites/Sprites_map_$n.png"))}
$html=Get-Content -LiteralPath (Join-Path $dir 'preview-template.html') -Raw
$html=$html.Replace('__DATA__',($data|ConvertTo-Json -Compress)).Replace('__STATS__',(Get-Content -LiteralPath (Join-Path $dir 'alpha-report.json') -Raw))
[IO.File]::WriteAllText((Join-Path $dir 'preview.html'),$html,[Text.UTF8Encoding]::new($false))

