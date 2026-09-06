[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Output,
    [string]$Resources = 'Maple2Storage/Resources',
    [string]$Index = 'NifToGltf/obj/research/simulator-item-index.json',
    [string]$Work = 'NifToGltf/obj/simulator-build'
)

# Run from the backend root with the extracted client sources described in README.
# Existing outputs are deliberately preserved. This script never publishes.
$ErrorActionPreference = 'Stop'
if ((Test-Path -LiteralPath $Output) -or (Test-Path -LiteralPath $Work)) {
    throw 'Choose fresh output and work directories.'
}
foreach ($path in @($Resources, $Index)) {
    if (!(Test-Path -LiteralPath $path)) { throw "Missing source: $path" }
}
function Invoke-Required([scriptblock]$Command) {
    & $Command
    if ($LASTEXITCODE -ne 0) { throw "Command failed with exit code $LASTEXITCODE" }
}
Invoke-Required { py -B NifToGltf/Diagnostics/simulator_library.py prepare --resources $Resources --index $Index --output "$Work/plan" }

# This candidate batch deliberately includes rejected previews. Its nonzero exit
# is retained in batch-report.json; the review gate below rejects missing approved assets.
dotnet run --project NifToGltf -- --native --batch --input $Resources --textures "$Resources/Models/Textures" --manifest "$Work/plan/plan.json" --output "$Work/models"
if (!(Test-Path -LiteralPath "$Work/models/native-manifest.json")) { throw 'No native manifest was produced.' }
Invoke-Required { py -B NifToGltf/Diagnostics/simulator_library.py catalog --output "$Work/plan" --release "$Work/models" }
Invoke-Required { dotnet run --project NifToGltf -- --native --batch --input $Resources --textures "$Resources/Models/Textures" --manifest NifToGltf/Diagnostics/simulator-hair-plan.json --output "$Work/hair" }
Invoke-Required { dotnet run --project NifToGltf -- --native --texture-batch --input "$Resources/SimulatorSources/Face" --output "$Work/faces" }
Invoke-Required { dotnet run --project NifToGltf -- --native --texture-batch --input "$Resources/SimulatorSources/Background" --output "$Work/backgrounds" }
Invoke-Required { py -B NifToGltf/Diagnostics/simulator_customization.py --xml "$Resources/SimulatorSources/Xml" --textures "$Work/faces" --release "$Work/models" }
Invoke-Required { py -B NifToGltf/Diagnostics/package_simulator.py --base "$Work/models" --hair "$Work/hair" --faces "$Work/faces" --backgrounds "$Work/backgrounds" --review NifToGltf/Diagnostics/simulator-review.json --output $Output }
Write-Output "Local release assembled at $Output. Run validation and the frontend acceptance checks before review."
