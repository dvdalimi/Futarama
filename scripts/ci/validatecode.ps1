<#
.SYNOPSIS
    Validates the code and assets to check for common patterns and usage that shouldn't be
    checked in.
.DESCRIPTION
    This currently checks both CSharp files and Unity serialized assets.

    - That Boo.Lang isn't used anywhere in the code. This is an autocomplete option
      that occurs when using Lists and other collections - the right thing to do
      is to use System.Collections
    - Validates that there are no empty doccomments (e.g. ///<summary></summary>)
    - All checked in profiles should not be marked custom
    - There are no references to hardcoded paths
    - There's only a single MixedRealityWorkspace per scene
    - There aren't any references to Camera.main

    Returns 0 if there are no issues, non-zero if there are.
.EXAMPLE
    .\validatecode.ps1 -Directory c:\path\to\MRTK\Assets -ChangesFile c:\path\to\changes\file.txt -RepoRoot c:\path\to\MRTK
#>
param(
    # The directory containing the code to validate. This won't be used if ChangesFile
    # is specified, but is always required because it's the fallback if
    # ChangesFile doesn't exist or isn't valid.
    [Parameter(Mandatory=$true)]
    [string]$Directory,

    # The filename containing the list of files to scope the code validation
    # to. This is useful in pull request validation when there isn't a need
    # to check every single file in the repo for changes (i.e. only the list
    # of changed files)
    #
    # If ChangesFile doesn't exist (i.e. not specified, null, is specified but
    # the actual file doesn't exist), then this defaults to checking for everything
    # in the repo.
    [string]$ChangesFile,

    # The directory containing the repo root. Used in conjunction with ChangesFile
    [string]$RepoRoot
)

<#
.SYNOPSIS
    Checks if the given file (at the given line number) contains a reference to Boo.Lang
    Returns true if such a reference exists.
#>
function CheckBooLang {
    [CmdletBinding()]
    param(
        [string]$FileName,
        [string[]]$FileContent,
        [int]$LineNumber
    )
    process {
        if ($FileContent[$LineNumber] -match "^using Boo\.Lang;") {
            Write-Host "An instance of Boo.Lang was found in $FileName at line $LineNumber "
            Write-Host "Use System.Collections instead."
            $true;
        }
        $false;
    }
}

<#
.SYNOPSIS
    Checks if the given file (at the given line number) contains an empty <returns></returns>
    or an <param ...></param> doccomment. These are typically added automatically by IDEs,
    but empty doccomments don't add value to code.
#>
function CheckEmptyDoccomment {
    [CmdletBinding()]
    param(
        [string]$FileName,
        [string[]]$FileContent,
        [int]$LineNumber
    )
    process {
        $tags = @("param", "returns")
        $containsEmptyDoccomment = $false

        foreach ($tag in $tags) {
            # This generates regexes that look like:
            # ///\s*<returns[\sa-zA-Z"=]*>\s*</returns>
            # which basically looks for an empty tag (allowing for alphanumeric param names
            # and values in the tag itself)
            $matcher = "///\s*<$tag[\sa-zA-Z0-9`"=]*>\s*</$tag>"
            if ($FileContent[$LineNumber] -match $matcher) {
                Write-Host "An empty doccomment was found in $FileName at line $LineNumber "
                Write-Host "Delete the line or add a description "
                Write-Host $FileContent[$LineNumber]
                $containsEmptyDoccomment = $true
            }
        }
        $containsEmptyDoccomment
    }
}

<#
.SYNOPSIS
    Checks if the given file (at the given line number) contains a comment of the type:
    //This comment doesn't have a space between // and This.
    There should be a space between // and the comment.
#>
function CheckSpacelessComments {
    [CmdletBinding()]
    param(
        [string]$FileName,
        [string[]]$FileContent,
        [int]$LineNumber
    )
    process {
        $hasIssue = $false

        # This regex looks for any non doccomment (i.e. //, not ///) where there isn't
        # a space after the //.
        # Explanation of the stuff inside the regex:
        # \s      - matches a space, to ensure that we don't capture cases like https://
        # //      - matches '//'
        # [^\s//] - matches a single character that is not a whitespace character and also
        #           not the '/' character (because doccomments like /// <summary> would
        #           otherwise get matched).
        $matcher = "\s//[^\s//]"
        if ($FileContent[$LineNumber] -match $matcher) {
            Write-Host "Comment in $FileName at line $LineNumber is missing a space after '//'"
            Write-Host $FileContent[$LineNumber]
            $hasIssue = $true
        }
        
        $hasIssue
    }
}

<#
.SYNOPSIS
    Checks if the given file (at the given line number) contains a reference to Camera.main
    Returns true if such a reference exists.
#>
function CheckMainCamera {
    [CmdletBinding()]
    param(
        [string]$FileName,
        [string[]]$FileContent,
        [int]$LineNumber
    )
    process {
        if ($FileName -notmatch "CameraCache.cs" -and $FileContent[$LineNumber] -match "Camera\.main") {
            Write-Host "An instance of Camera.main was found in $FileName at line $LineNumber "
            Write-Host "Use CameraCache.Main instead."
            $true;
        }
        $false;
    }
}

<#
.SYNOPSIS
    Checks if the given profile contains references to Assembly-CSharp, often indicative of invalid reference.
    Returns true if such a reference exists.
#>
function CheckAssemblyCSharp {
    [CmdletBinding()]
    param(
        [string]$FileName,
        [string[]]$FileContent,
        [int]$LineNumber
    )
    process {
        if ($FileName -and $FileContent[$LineNumber] -match "Assembly-CSharp") {
            Write-Host "An instance of 'Assembly-CSharp' was found in $FileName at line $LineNumber"
            Write-Host "Please update this to reference the correct assembly."
            $true
        }
        $false
    }
}

<#
.SYNOPSIS
    Checks if the given profile is marked as a custom profile.
    Returns true if such a reference exists.
#>
function CheckCustomProfile {
    [CmdletBinding()]
    param(
        [string]$FileName,
        [string[]]$FileContent,
        [int]$LineNumber
    )
    process {
        if ($FileName -notmatch "Examples" -and $FileContent[$LineNumber] -match "isCustomProfile: 1") {
            Write-Host "An instance of 'isCustomProfile: 1' was found in $FileName at line $LineNumber"
            Write-Host "Please update this to 'isCustomProfile: 0' instead."
            $true
        }
        $false
    }
}

# This dictionary contains the list of all allowed hardcoded path exceptions
# You should NOT add things to this list unless you believe that your scenario should only work
# when the MRTK is placed in a specific location in the root assets folder. This is only valid
# in two locations currently:
# 1) Test code
# 2) Profile cloning code - this code needs to choose some reasonable default, preferring
#    "Assets/MixedRealityToolkit.Generated/CustomProfiles" as that default location
$HardcodedPathExceptions = @{
    "MixedRealityToolkitConfigurationProfileInspector.cs" = @(
        'var newProfile = profile.CreateAsset("Assets/MixedRealityToolkit.Generated/CustomProfiles") as MixedRealityToolkitConfigurationProfile;'
    );
    # This exception should be deleted once https://github.com/microsoft/MixedRealityToolkit-Unity/issues/6448 is resolved
    "MRTKExamplesHub.unity" = @(
        'Path: Assets/MRTK/Examples/Experimental/ExamplesHub/Scenes/MRTKExamplesHubMainMenu.unity'
        'value: Assets/MRTK/Examples/Experimental/ExamplesHub/Scenes/MRTKExamplesHubMainMenu.unity'
    );
    # This exception should be deleted once https://github.com/microsoft/MixedRealityToolkit-Unity/issues/6448 is resolved
    "MRTKExamplesHubMainMenu.unity" = @(
        'value: Assets/MRTK/Examples/Demos/UX/Tooltips/Scenes/TooltipExamples.unity'
        'value: Assets/MRTK/Examples/Demos/HandTracking/Scenes/HandMenuExamples.unity'
        'value: Assets/MRTK/Examples/Demos/HandTracking/Scenes/HandInteractionExamples.unity'
        'value: Assets/MRTK/Examples/Demos/EyeTracking/Scenes/EyeTrackingDemo-04-TargetPositioning.unity'
        'value: Assets/MRTK/Examples/Demos/EyeTracking/Scenes/EyeTrackingDemo-03-Navigation.unity'
        'value: Assets/MRTK/Examples/Demos/EyeTracking/Scenes/EyeTrackingDemo-02-TargetSelection.unity'
        'value: Assets/MRTK/Examples/Demos/UX/Slate/SlateExample.unity'
        'value: Assets/MRTK/Examples/Demos/UX/PressableButton/Scenes/PressableButtonExample.unity'
        'value: Assets/MRTK/Examples/Demos/StandardShader/Scenes/ClippingExamples.unity'
        'value: Assets/MRTK/Examples/Demos/UX/Slider/Scenes/SliderExample.unity'
        'value: Assets/MRTK/Examples/Demos/UX/BoundingBox/Scenes/BoundingBoxExamples.unity'
        'value: Assets/MRTK/Examples/Demos/EyeTracking/Scenes/EyeTrackingDemo-05-Visualizer.unity'
        'value: Assets/MRTK/Examples/Demos/HandTracking/Scenes/NearMenuExamples.unity'
        'value: Assets/MRTK/Examples/Demos/StandardShader/Scenes/MaterialGallery.unity'
    );
}

<#
.SYNOPSIS
    Checks if the line contains a hardcoded path i.e. "Assets/MRTK/Examples"
    Hardcoded paths are generally not allowed, except in certain cases such as:

    - Tests cases (tests are not run by consumers of the toolkit)
    - Default saved location of custom profiles (it will default to a folder in the Assets/ location)
#>
function CheckHardcodedPath {
    [CmdletBinding()]
    param(
        [string]$FileName
    )
    process {
        # Some files have total exceptions (i.e. ones that deal with profile cloning or for tests)
        if ($FileName -match "Assets.MixedRealityToolkit\.Tests" -or
            $FileName -match "MixedRealityProfileCloneWindow\.cs") {
            return $false
        }

        $results = Select-String -Pattern "Assets.MixedRealityToolkit" $FileName -AllMatches
        $containsIssue = $false
        $relativeFileName = Split-Path $FileName -leaf

        foreach ($match in $results) {
            $trimmed = $match.Line.trim()
            if (-Not $HardcodedPathExceptions.Contains($relativeFileName) -Or
                    ($HardcodedPathExceptions.Contains($relativeFileName) -And
                    -Not $HardcodedPathExceptions[$relativeFileName].Contains($trimmed))) {
                Write-Host "Hardcoded path detected in $FileName - $trimmed"
                Write-Host "Please delete usage of hardcoded path or add an exception in HardcodedPathExceptions"
                Write-Host "if this is a valid hardcoded path usage."
                $containsIssue = $true
            }
        }
        $containsIssue
    }
}

<#
.SYNOPSIS
    Checks if the file has a corresponding meta checked in.
    Returns true if the meta is missing.
#>
function CheckForMetaFile {
    [CmdletBinding()]
    param(
        [string]$FileName
    )
    process {
        if (-not (Test-Path ($FileName + ".meta"))) {
            Write-Warning "Meta file missing for $FileName. Please be sure to check it in alongside this file."
            $true;
        }
        $false;
    }
}

<#
.SYNOPSIS
    Checks if the file has a corresponding meta checked in.
    Returns true if the meta is missing.
#>
function CheckForActualFile {
    [CmdletBinding()]
    param(
        [string]$FileName
    )
    process {
        # Remove .meta from path
        if (-not (Test-Path $FileName.Substring(0, $FileName.LastIndexOf('.')))) {
            Write-Warning "Actual file missing for meta file $FileName. Please be sure to check it in or remove this meta."
            $true;
        }
        $false;
    }
}

<#
.SYNOPSIS
    Returns true if the given line is a namespace declaration
#>
function IsNamespace {
    [CmdletBinding()]
    param(
        [string]$Line
    )
    process {
        if (($Line -match "^namespace\sMicrosoft\.MixedReality\.Toolkit") -or
            ($Line -match "^namespace\sMicrosoft\.Windows\.MixedReality")) {
            $true;
        }
        $false;
    }
}

<#
.SYNOPSIS
    Given a full filename path, this returns the MRTK project relative path
    of the file and normalizes the separators to /.
    For example, given D:\src\MixedRealityToolkit-Unity\Assets\MRTK\Services\DiagnosticsSystem\File.cs,
    this would return Assets/MRTK/Services/DiagnosticsSystem/File.cs.
    Note that this function asssumes the Assets/MRTK prefix for all of the MRTK code,
    and if this ever changes this function would need to be updated to accomodate that.
#>
function GetProjectRelativePath {
    [CmdletBinding()]
    param(
        [string]$Filename
    )
    process {
        $normalizedFileName = $FileName.Replace("\", "/")
        $assetPathStartIndex = $normalizedFileName.IndexOf("Assets/MRTK")
        $assetFileName = $normalizedFileName.SubString($assetPathStartIndex)
        $assetFileName
    }
}

# This set contains all of the currently allowed InitializeOnLoad handlers# in MRTK.
# InitializeOnLoad handlers have a fairly dangerous impact on the inner loop speed of anyone
# using the MRTK, as they add milliseconds of time after each compile and prior to entering play mode.
# While individual handlers may not be that significant, the sum total of time across all handlers
# (which run serially) causes noticable delays in responsiveness in the Unity editor.
$InitializeOnLoadExceptions = [System.Collections.Generic.HashSet[String]]@(
    "Assets/MRTK/Core/Inspectors/MixedRealityToolkitFacadeHandler.cs",
    "Assets/MRTK/Core/Inspectors/PropertyDrawers/SceneInfoUtils.cs",
    "Assets/MRTK/Core/Inspectors/ServiceInspectors/ServiceFacadeInspector.cs",
    "Assets/MRTK/Core/Inspectors/Setup/MixedRealityEditorSettings.cs",
    "Assets/MRTK/Core/Inspectors/Utilities/MixedRealityProfileUtility.cs",
    "Assets/MRTK/Core/Services/MixedRealityToolkit.cs",
    "Assets/MRTK/Core/Utilities/MixedRealityPlayspace.cs",
    "Assets/MRTK/Core/Utilities/WindowsApiChecker.cs",
    "Assets/MRTK/Core/Utilities/Async/Internal/SyncContextUtility.cs",
    "Assets/MRTK/Core/Utilities/Editor/EditorProjectUtilities.cs",
    "Assets/MRTK/Core/Utilities/Editor/Setup/MixedRealityToolkitFiles.cs",
    "Assets/MRTK/Core/Utilities/Editor/USB/USBDeviceListener.cs",
    "Assets/MRTK/Providers/LeapMotion/Editor/LeapMotionConfigurationChecker.cs",
    "Assets/MRTK/Providers/Oculus/XRSDK/Editor/OculusXRSDKConfigurationChecker.cs",
    "Assets/MRTK/Providers/UnityAR/Editor/UnityARConfigurationChecker.cs",
    "Assets/MRTK/Providers/WindowsMixedReality/Shared/Editor/WindowsMixedRealityConfigurationChecker.cs",
    "Assets/MRTK/Providers/WindowsMixedReality/XRSDK/Editor/WindowsMixedRealityXRSDKConfigurationChecker.cs",
    "Assets/MRTK/Providers/XRSDK/Editor/XRSDKConfigurationChecker.cs"
)

<#
.SYNOPSIS
    Checks if the given file (at the given line number) contains a non-exempt
    InitializeOnLoad handler.
#>
function CheckInitializeOnLoad {
    [CmdletBinding()]
    param(
        [string]$FileName,
        [string[]]$FileContent,
        [int]$LineNumber
    )
    process {
        $hasIssue = $false
        if ($FileContent[$LineNumber] -match "InitializeOnLoad") {
            $assetFileName = GetProjectRelativePath($FileName)
            if (-Not $InitializeOnLoadExceptions.Contains($assetFileName)) {
                #Write-Warning "A new InitializeOnLoad handler was introduced in: $assetFileName. An exception may be added "
                #Write-Warning "to `$InitializeOnLoadExceptions after discussion with the rest of the team."
                $hasIssue = $true

                Write-Host "`"$assetFileName`","
            }
        }
        $hasIssue
    }
}

function CheckScript {
    [CmdletBinding()]
    param(
        [string]$FileName
    )
    process {
        # Each line of each script is checked by all of the validators above - this ensures that in
        # a single pass, we'll get all of the issues highlighted all at once, rather than
        # repeatedly running this script, discovering a single issue, fixing it, and then
        # re-running the script
        $containsIssue = $false
        $containsNamespaceDeclaration = $false;
        $fileContent = Get-Content $FileName
        for ($i = 0; $i -lt $fileContent.Length; $i++) {
            if (CheckBooLang $FileName $fileContent $i) {
                $containsIssue = $true
            }
            if (CheckEmptyDoccomment $FileName $fileContent $i) {
                $containsIssue = $true
            }
            if (CheckMainCamera $FileName $fileContent $i) {
                $containsIssue = $true
            }
            if (CheckSpacelessComments $FileName $fileContent $i) {
                $containsIssue = $true
            }
            if (CheckInitializeOnLoad $FileName $fileContent $i) {
                $containsIssue = $true
            }
            $containsNamespaceDeclaration = $containsNamespaceDeclaration -or (IsNamespace $fileContent[$i])
        }

        # Only validate that there is a namespace declaration if it's not an AssemblyInfo.cs file.
        # These do not contain namespace declarations.
        if ((-not $containsNamespaceDeclaration) -and ($FileName -notmatch "AssemblyInfo.cs$"))
        {
            Write-Warning "$FileName is missing a namespace declaration (i.e. missing namespace Microsoft.MixedReality.Toolkit.*)"
            $containsIssue = $true;
        }

        if (CheckHardcodedPath $FileName) {
            $containsIssue = $true
        }

        if (CheckForMetaFile $FileName) {
            $containsIssue = $true
        }

        $containsIssue
    }
}

function CheckAsset {
    [CmdletBinding()]
    param(
        [string]$FileName
    )
    process {
        # Each line of each asset is checked by all of the validators above - this ensures that in
        # a single pass, we'll get all of the issues highlighted all at once, rather than
        # repeatedly running this script, discovering a single issue, fixing it, and then
        # re-running the script
        $containsIssue = $false
        $fileContent = Get-Content $FileName
        for ($i = 0; $i -lt $fileContent.Length; $i++) {
            if (CheckCustomProfile $FileName $fileContent $i) {
                $containsIssue = $true
            }
            if (CheckAssemblyCSharp $FileName $fileContent $i) {
                $containsIssue = $true
            }
        }

        # Filter out the ProjectSettings .asset files, which don't have a meta file and don't need one.
        if ((-not $FileName.Contains("\ProjectSettings\")) -and (CheckForMetaFile $FileName)) {
            $containsIssue = $true
        }

        $containsIssue
    }
}

function CheckUnityScene {
    [CmdletBinding()]
    param(
        [string]$FileName
    )
    process {
        $containsIssue = $false

        # Checks if there is more than one MixedRealityPlayspace objects in each example unity scene
        $MatchesPlayspaces = Select-String MixedRealityPlayspace $FileName -AllMatches
        $NumPlayspaces = $MatchesPlayspaces.Matches.Count

        if ($NumPlayspaces -gt 1){
            Write-Warning "There are multiple MixedRealityPlayspace objects in $FileName, delete the extra playspaces from the unity scene."
            $containsIssue = $true
        }

        if (CheckHardcodedPath $FileName) {
            $containsIssue = $true
        }

        if (CheckForMetaFile $FileName) {
            $containsIssue = $true
        }

        $containsIssue
    }
}

# This set contains all of the currently allowed asmdefs in the MRTK
# If you're reading this, it's probably because you've added a new asmdef
# and you're seeing a CI build/PR validation failure. This is because we've
# added a roadblock to force discussion of new asmdef creation to ensure that we're
# not creating a lot of tiny ones.
# There's non-trivial overhead to the addition of each asmdef (i.e. each asmdef will
# create build overhead associated with all of the stuff that happens before the actual code
# inside gets compiled.)
# In certain cases (especially lighting up a new platform/provider) this will be a necessary
# addition, but in others it may make more sense to put group the code with another existing
# binary that has a lot of overlap.
# Either way, this is an explicit speed bump being added to force discussion at future times.
$AsmDefExceptions = [System.Collections.Generic.HashSet[String]]@(
    "Assets/MRTK/Core/Microsoft.MixedReality.Toolkit.asmdef",
    "Assets/MRTK/Core/Extensions/EditorClassExtensions/Microsoft.MixedReality.Toolkit.Editor.ClassExtensions.asmdef",
    "Assets/MRTK/Core/Inspectors/Microsoft.MixedReality.Toolkit.Editor.Inspectors.asmdef",
    "Assets/MRTK/Core/Inspectors/ServiceInspectors/Microsoft.MixedReality.Toolkit.Editor.ServiceInspectors.asmdef",
    "Assets/MRTK/Core/Providers/InputAnimation/MixedRealityToolkit.Services.InputAnimation.asmdef",
    "Assets/MRTK/Core/Providers/InputSimulation/Microsoft.MixedReality.Toolkit.Services.InputSimulation.asmdef",
    "Assets/MRTK/Core/Providers/InputSimulation/Editor/Microsoft.MixedReality.Toolkit.Services.InputSimulation.Editor.asmdef",
    "Assets/MRTK/Core/Providers/ObjectMeshObserver/Microsoft.MixedReality.Toolkit.Providers.ObjectMeshObserver.asmdef",
    "Assets/MRTK/Core/Utilities/Async/Microsoft.MixedReality.Toolkit.Async.asmdef",
    "Assets/MRTK/Core/Utilities/BuildAndDeploy/Microsoft.MixedReality.Toolkit.Editor.BuildAndDeploy.asmdef",
    "Assets/MRTK/Core/Utilities/Editor/Microsoft.MixedReality.Toolkit.Editor.Utilities.asmdef",
    "Assets/MRTK/Core/Utilities/Gltf/Microsoft.MixedReality.Toolkit.Gltf.asmdef",
    "Assets/MRTK/Core/Utilities/Gltf/Serialization/Importers/Microsoft.MixedReality.Toolkit.Gltf.Importers.asmdef",
    "Assets/MRTK/Examples/Microsoft.MixedReality.Toolkit.Examples.asmdef",
    "Assets/MRTK/Examples/Demos/Gltf/Microsoft.MixedReality.Toolkit.Demos.Gltf.asmdef",
    "Assets/MRTK/Examples/Demos/Gltf/Scripts/Editor/Microsoft.MixedReality.Toolkit.Demos.Gltf.Inspectors.asmdef",
    "Assets/MRTK/Examples/Demos/StandardShader/Scripts/Editor/Demos.StandardShader.Inspectors.asmdef",
    "Assets/MRTK/Examples/Demos/Utilities/InspectorFields/Microsoft.MixedReality.Toolkit.Demos.InspectorFields.asmdef",
    "Assets/MRTK/Examples/Demos/Utilities/InspectorFields/Inspectors/Demos.InspectorFields.Inspectors.asmdef",
    "Assets/MRTK/Examples/Demos/UX/Interactables/Microsoft.MixedReality.Toolkit.Demos.UX.Interactables.asmdef",
    "Assets/MRTK/Examples/Experimental/Dwell/Editor/Microsoft.MixedReality.Examples.Editor.Dwell.asmdef",
    "Assets/MRTK/Extensions/HandPhysicsService/Microsoft.MixedReality.Toolkit.Extensions.HandPhysics.asmdef",
    "Assets/MRTK/Extensions/LostTrackingService/Microsoft.MixedReality.Toolkit.Extensions.Tracking.asmdef",
    "Assets/MRTK/Extensions/LostTrackingService/Editor/Microsoft.MixedReality.Toolkit.Extensions.Tracking.Editor.asmdef",
    "Assets/MRTK/Extensions/SceneTransitionService/Microsoft.MixedReality.Toolkit.Extensions.SceneTransitionService.asmdef",
    "Assets/MRTK/Providers/LeapMotion/Microsoft.MixedReality.Toolkit.Providers.LeapMotion.asmdef",
    "Assets/MRTK/Providers/LeapMotion/Editor/Microsoft.MixedReality.Toolkit.LeapMotion.Editor.asmdef",
    "Assets/MRTK/Providers/Oculus/XRSDK/Microsoft.MixedReality.Toolkit.Providers.XRSDK.Oculus.asmdef",
    "Assets/MRTK/Providers/Oculus/XRSDK/Editor/Microsoft.MixedReality.Toolkit.XRSDK.Oculus.Editor.asmdef",
    "Assets/MRTK/Providers/OpenVR/Microsoft.MixedReality.Toolkit.Providers.OpenVR.asmdef",
    "Assets/MRTK/Providers/UnityAR/Microsoft.MixedReality.Toolkit.Providers.UnityAR.asmdef",
    "Assets/MRTK/Providers/UnityAR/Editor/Microsoft.MixedReality.Toolkit.UnityAR.Editor.asmdef",
    "Assets/MRTK/Providers/Windows/Microsoft.MixedReality.Toolkit.Providers.WindowsVoiceInput.asmdef",
    "Assets/MRTK/Providers/WindowsMixedReality/Shared/Microsoft.MixedReality.Toolkit.Providers.WindowsMixedReality.Shared.asmdef",
    "Assets/MRTK/Providers/WindowsMixedReality/Shared/Editor/Microsoft.MixedReality.Toolkit.WMR.Editor.asmdef",
    "Assets/MRTK/Providers/WindowsMixedReality/XR2018/Microsoft.MixedReality.Toolkit.Providers.WindowsMixedReality.asmdef",
    "Assets/MRTK/Providers/WindowsMixedReality/XRSDK/Microsoft.MixedReality.Toolkit.Providers.XRSDK.WMR.asmdef",
    "Assets/MRTK/Providers/WindowsMixedReality/XRSDK/Editor/Microsoft.MixedReality.Toolkit.XRSDK.WMR.Editor.asmdef",
    "Assets/MRTK/Providers/XRSDK/Microsoft.MixedReality.Toolkit.Providers.XRSDK.asmdef",
    "Assets/MRTK/Providers/XRSDK/Editor/Microsoft.MixedReality.Toolkit.XRSDK.Editor.asmdef",
    "Assets/MRTK/SDK/Microsoft.MixedReality.Toolkit.SDK.asmdef",
    "Assets/MRTK/SDK/Editor/Microsoft.MixedReality.Toolkit.SDK.Editor.asmdef",
    "Assets/MRTK/SDK/Experimental/Editor/Microsoft.MixedReality.Toolkit.SDK.Experimental.Editor.asmdef",
    "Assets/MRTK/Services/BoundarySystem/XR2018/Microsoft.MixedReality.Toolkit.Services.BoundarySystem.asmdef",
    "Assets/MRTK/Services/CameraSystem/Microsoft.MixedReality.Toolkit.Services.CameraSystem.asmdef",
    "Assets/MRTK/Services/DiagnosticsSystem/Microsoft.MixedReality.Toolkit.Services.DiagnosticsSystem.asmdef",
    "Assets/MRTK/Services/InputSystem/Microsoft.MixedReality.Toolkit.Services.InputSystem.asmdef",
    "Assets/MRTK/Services/InputSystem/Editor/Microsoft.MixedReality.Toolkit.Services.InputSystem.Editor.asmdef",
    "Assets/MRTK/Services/SceneSystem/Microsoft.MixedReality.Toolkit.Services.SceneSystem.asmdef",
    "Assets/MRTK/Services/SpatialAwarenessSystem/Microsoft.MixedReality.Toolkit.Services.SpatialAwarenessSystem.asmdef",
    "Assets/MRTK/Services/TeleportSystem/Microsoft.MixedReality.Toolkit.Services.TeleportSystem.asmdef",
    "Assets/MRTK/Tests/EditModeTests/Microsoft.MixedReality.Toolkit.Tests.EditModeTests.asmdef",
    "Assets/MRTK/Tests/PlayModeTests/Microsoft.MixedReality.Toolkit.Tests.PlayModeTests.asmdef",
    "Assets/MRTK/Tests/TestUtilities/Microsoft.MixedReality.Toolkit.Tests.Utilities.asmdef",
    "Assets/MRTK/Tools/Microsoft.MixedReality.Toolkit.Tools.asmdef",
    "Assets/MRTK/Tools/MSBuild/Microsoft.MixedReality.Toolkit.MSBuild.asmdef",
    "Assets/MRTK/Tools/RuntimeTools/Tools/Microsoft.MixedReality.Toolkit.Tools.Runtime.asmdef"
)

function CheckAsmDef {
    [CmdletBinding()]
    param(
        [string]$FileName
    )
    process {
        $containsIssue = $false
        $assetFileName = GetProjectRelativePath($FileName)
        if (-Not $AsmDefExceptions.Contains($assetFileName)) {
            Write-Warning "New Asmdef was added but is not on the allowed list: $assetFileName. An exception can be added to `$AsmDefExceptions "
            Write-Warning "after a dicussion with the rest of the team determining if the asmdef is necessary."
            $containsIssue = $true
        }
        $containsIssue
    }
}

# If the file containing the list of changes was provided and actually exists,
# this validation should scope to only those changed files.
if ($ChangesFile -and (Test-Path $ChangesFile -PathType leaf)) {
    # TODO(https://github.com/microsoft/MixedRealityToolkit-Unity/issues/7022)
    # There may be ways to configure common modules so that paths like this aren't required
    Import-Module -Force (Resolve-Path("$RepoRoot\scripts\ci\common.psm1"))

    Write-Host "Checking only changed files for code issues: $ChangesFile"
    $changedFiles = GetChangedFiles -Filename $ChangesFile -RepoRoot $RepoRoot
    ForEach ($changedFile in $changedFiles) {
        Write-Host "Checking file: $changedFile"
        if (((IsCSharpFile -Filename $changedFile) -and (CheckScript $changedFile)) -or
            ((IsAssetFile -Filename $changedFile) -and (CheckAsset $changedFile)) -or
            ((IsUnityScene -Filename $changedFile) -and (CheckUnityScene $changedFile)) -or
            ((IsMetaFile -Filename $changedFile) -and (CheckForActualFile $changedFile)) -or
            ((IsAsmDef -Filename $changedFile) -and (CheckAsmDef $changedFile))) {
            $containsIssue = $true;
        }
    }
}
else {
    Write-Host "Checking $Directory for common code issues"

    $codeFiles = Get-ChildItem $Directory *.cs -Recurse | Select-Object FullName
    $containsIssue = $false
    foreach ($codeFile in $codeFiles) {
        if (CheckScript $codeFile.FullName) {
            $containsIssue = $true
        }
    }

    $codeFiles = Get-ChildItem $Directory *.asset -Recurse | Select-Object FullName
    foreach ($codeFile in $codeFiles) {
        if (CheckAsset $codeFile.FullName) {
            $containsIssue = $true
        }
    }

    # Check all Unity scenes for extra MixedRealityPlayspace objects
    $codeFiles = Get-ChildItem $Directory *.unity -Recurse | Select-Object FullName
    foreach ($codeFile in $codeFiles) {
        if (CheckUnityScene $codeFile.FullName) {
            $containsIssue = $true
        }
    }

    $metas = Get-ChildItem $Directory *.meta -File -Recurse | Select-Object FullName
    foreach ($meta in $metas) {
        if (CheckForActualFile $meta.FullName) {
            $containsIssue = $true
        }
    }

    $asmdefs = Get-ChildItem $Directory *.asmdef -File -Recurse | Select-Object FullName
    foreach ($asmdef in $asmdefs) {
        if (CheckAsmDef $asmdef.FullName) {
            $containsIssue = $true
        }
    }
}

$folders = Get-ChildItem $Directory -Directory -Recurse | Select-Object FullName
foreach ($folder in $folders) {
    if (CheckForMetaFile $folder.FullName) {
        $containsIssue = $true
    }
}

if ($containsIssue) {
    Write-Output "Issues found, please see above for details"
    exit 1;
}
else {
    Write-Output "No issues found"
    exit 0;
}
