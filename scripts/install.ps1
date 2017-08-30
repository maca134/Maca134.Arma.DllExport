param($installPath, $toolsPath, $package, $project)

$targetFileName = 'Maca134.Arma.DllExport.targets'
$targetFileName = [IO.Path]::Combine($toolsPath, $targetFileName)
$targetUri = New-Object Uri -ArgumentList $targetFileName, [UriKind]::Absolute

$msBuildV4Name = 'Microsoft.Build, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a';
$msBuildV4 = [System.Reflection.Assembly]::LoadWithPartialName($msBuildV4Name)

if(!$msBuildV4) {
    throw New-Object System.IO.FileNotFoundException("Could not load $msBuildV4Name.");
}

$projectCollection = $msBuildV4.GetType('Microsoft.Build.Evaluation.ProjectCollection')

# change the reference to RGiesecke.DllExport.Metadata.dll to not be copied locally

$project.Object.References | ? { 
    $_.Name -ieq "Maca134.Arma.DllExport" 
} | % {
    if($_ | Get-Member | ? {$_.Name -eq "CopyLocal"}){
        $_.CopyLocal = $false
    }
}

$projects =  $projectCollection::GlobalProjectCollection.GetLoadedProjects($project.FullName)
$projects |  % {
    $currentProject = $_

    # remove imports of RGiesecke.DllExport.targets from this project 
    $currentProject.Xml.Imports | ? {
        return ("Maca134.Arma.DllExport.targets" -ieq [IO.Path]::GetFileName($_.Project))
    }  | % {  
        $currentProject.Xml.RemoveChild($_);
    }

    # remove the properties DllExportAttributeFullName and DllExportAttributeAssemblyName
    $currentProject.Xml.Properties | ? {
        $_.Name -eq "DllExportAttributeFullName" -or $_.Name -eq "DllExportAttributeAssemblyName"
    } | % {
        $_.Parent.RemoveChild($_)
    }

    $projectUri = New-Object Uri -ArgumentList $currentProject.FullPath, [UriKind]::Absolute
    $relativeUrl = $projectUri.MakeRelative($targetUri)
    $import = $currentProject.Xml.AddImport($relativeUrl)
    $import.Condition = "Exists('$relativeUrl')";
}

