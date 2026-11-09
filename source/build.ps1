$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$output = Join-Path $root '鸣潮终焉矩阵配队工具.exe'
$refs = @('System.dll', 'System.Core.dll', 'System.Drawing.dll', 'System.Web.Extensions.dll', 'System.Xaml.dll', 'WPF\WindowsBase.dll', 'WPF\PresentationCore.dll', 'WPF\PresentationFramework.dll')
$compilerArgs = @('/nologo', '/target:winexe', '/platform:anycpu', '/optimize+', '/utf8output', ('/out:' + $output), ('/win32manifest:' + (Join-Path $PSScriptRoot 'app.manifest')))
foreach ($reference in $refs) { $compilerArgs += '/reference:' + (Join-Path $framework $reference) }
$compilerArgs += '/resource:' + (Join-Path $root 'assets\characters.json') + ',characters.json'
$characters = Get-Content -Raw -Encoding utf8 -LiteralPath (Join-Path $root 'assets\characters.json') | ConvertFrom-Json
foreach ($character in $characters) {
 $image = Join-Path $root ('assets\' + $character.Id + '.png')
 if (-not (Test-Path -LiteralPath $image)) { throw ('Missing image: ' + $character.Name) }
 $compilerArgs += '/resource:' + $image + ',portrait.' + $character.Id
}
$compilerArgs += '/resource:' + (Join-Path $root 'assets\recognition-variants.json') + ',recognition-variants.json'
$variants = Get-Content -Raw -Encoding utf8 -LiteralPath (Join-Path $root 'assets\recognition-variants.json') | ConvertFrom-Json
foreach ($variant in $variants) { $compilerArgs += '/resource:' + (Join-Path $root ('assets\' + $variant.File)) + ',' + $variant.Resource }
$compilerArgs += '/resource:' + (Join-Path $root 'assets\official-recommendations.json') + ',official-recommendations.json'
$compilerArgs += '/resource:' + (Join-Path $root 'assets\element-recognition.json') + ',element-recognition.json'
$elements = Get-Content -Raw -Encoding utf8 -LiteralPath (Join-Path $root 'assets\element-recognition.json') | ConvertFrom-Json
foreach ($element in $elements) { $compilerArgs += '/resource:' + (Join-Path $root ('assets\' + $element.File)) + ',' + $element.Resource }
$compilerArgs += (Join-Path $PSScriptRoot 'Matrix.cs')
$compilerArgs += (Join-Path $PSScriptRoot 'ScreenshotRecognition.cs')
$compilerArgs += (Join-Path $PSScriptRoot 'ScreenshotImportWindow.cs')
$compilerArgs += (Join-Path $PSScriptRoot 'ClipboardScreenshots.cs')
$compilerArgs += (Join-Path $PSScriptRoot 'NativeClipboardImages.cs')
$compilerArgs += (Join-Path $PSScriptRoot 'ClipboardBridgeTests.cs')
$compilerArgs += (Join-Path $PSScriptRoot 'ScreenshotSessionTests.cs')
$compilerArgs += '/resource:' + (Join-Path $root 'assets\character-recommendation-metadata.json') + ',character-recommendation-metadata.json'
$compilerArgs += (Join-Path $PSScriptRoot 'CharacterRecommendationMetadata.cs')
$compilerArgs += (Join-Path $PSScriptRoot 'TeamRecommendations.cs')
$compilerArgs += (Join-Path $PSScriptRoot 'RecommendationSourcesWindow.cs')
$compilerArgs += (Join-Path $PSScriptRoot 'TeamRecommendationTests.cs')
$compilerArgs += (Join-Path $PSScriptRoot 'RecommendationRuleTests.cs')
$compilerArgs += (Join-Path $PSScriptRoot 'RoverAndExtraUseTests.cs')
& (Join-Path $framework 'csc.exe') @compilerArgs
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Write-Output $output
