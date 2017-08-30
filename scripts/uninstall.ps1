param($installPath, $toolsPath, $package, $project)

$targetFileName = 'Maca134.Arma.DllExport.targets'

$projects = Get-DllExportMsBuildProjectsByFullName($project.FullName)

return $projects |  % {
    $currentProject = $_
    $currentProject.Xml.Imports | ? {
        $targetFileName -ieq [System.IO.Path]::GetFileName($_.Project)
    }  | % {  
        $currentProject.Xml.RemoveChild($_)
    }
}