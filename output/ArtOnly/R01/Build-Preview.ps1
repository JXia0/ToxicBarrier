$dir=Join-Path (Get-Location) 'output/ArtOnly/R01'
$data=@{}
foreach($name in @('stone','gel','fang','tower1','tower2','tower3')){$data[$name]='data:image/png;base64,'+[Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $dir "$name.png")))}
foreach($name in @('map1','map2','map3','wood','knight','goblin','eye','skeleton','house','hud')){$data[$name]='data:image/png;base64,'+[Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $dir "references/$name.png")))}
$data['review']='data:image/jpeg;base64,'+[Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $dir 'references/CurrentAssetReview.jpg')))
$html=Get-Content -LiteralPath (Join-Path $dir 'preview-template.html') -Raw
$html=$html.Replace('__DATA__',($data|ConvertTo-Json -Compress)).Replace('__ASSETS__',(Get-Content -LiteralPath (Join-Path $dir 'alpha-report.json') -Raw))
[IO.File]::WriteAllText((Join-Path $dir 'preview.html'),$html,[Text.UTF8Encoding]::new($false))

