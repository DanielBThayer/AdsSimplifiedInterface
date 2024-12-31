$xml = [Xml] (Get-Content "src\\ADS Simplified Interface.csproj")
$version = $xml.Project.PropertyGroup.Version
$versionParts = $version.split(".")
$versionParts[-1] = [string]([int]($versionParts[-1]) + 1)
$ofs='.'
$newVersion = [string]$versionParts
$xml.Project.PropertyGroup.Version = $newVersion
$xml.Project.PropertyGroup.FileVersion = $newVersion
$xml.Project.PropertyGroup.AssemblyVersion = $newVersion
$xml.Save("src\\ADS Simplified Interface.csproj")