param([string]$config)
$solutionVer = $null
$rootDir = Get-Location
$srcDir = Get-ChildItem ./src
$testDir = Get-ChildItem ./test
if(!$config){
    $config = "Debug"
}
Write-Output "Building $config configuration"
Write-Output "--------------------------------------"
#loop through src folders and restore & build
foreach ($folder in $srcDir) {
    $p = Join-Path -Path $folder.FullName -ChildPath 'project.json';
    
    # only restore projects -> folders with a project.json file 
    if (Test-Path $p -PathType Leaf) {
        Set-Location $folder.FullName
        dotnet restore
        dotnet build --configuration $config
    }
}
#loop through test folders and restore & build
foreach ($folder in $testDir) {
    $p = Join-Path -Path $folder.FullName -ChildPath 'project.json';
    
    # only restore projects -> folders with a project.json file 
    if (Test-Path $p -PathType Leaf) {
        Set-Location $folder.FullName
        dotnet restore
        dotnet build --configuration $config
    }
}

# loop through projects under ./src
foreach ($folder in $srcDir) {
    $p = Join-Path -Path $folder.FullName -ChildPath 'project.json';
    
    # only build projects -> folders with a project.json file 
    if (Test-Path $p -PathType Leaf) {
        Write-Output ""
        Write-Output "Next project: $folder.Name"
        Set-Location $folder.FullName
        
        # find the test project, if one exists, and run each
        $testFolderPath = "..\..\test\" + $folder.Name + ".Tests"
        if (Test-Path $testFolderPath -PathType Container){
            Set-Location $testFolderPath
            
            dotnet test --configuration $config -trait "TestType=Unit"
            
            Set-Location $folder.FullName
        }
        else{
            Write-Output ""
            Write-Output "test path not found"
        }
    }
}
Set-Location $rootDir
Write-Output Done