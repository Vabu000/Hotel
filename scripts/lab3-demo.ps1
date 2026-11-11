# DVO-L3-7F2A91
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$namespace = 'hotel-reservation-system'
$reportDirectory = Join-Path $repositoryRoot 'reports'
$reportPath = Join-Path $reportDirectory 'lab3-demo.txt'
$utf8NoBom = [System.Text.UTF8Encoding]::new($false)

New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null
[System.IO.File]::WriteAllText($reportPath, "# DVO-L3-7F2A91`n", $utf8NoBom)
Start-Transcript -Path $reportPath -Append | Out-Null
$originalImageTag = $env:IMAGE_TAG

function Invoke-RequiredCommand {
    param(
        [Parameter(Mandatory)]
        [string] $File,
        [Parameter()]
        [string[]] $Arguments = @()
    )

    & $File @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$File $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Invoke-Kubectl {
    param([Parameter(Mandatory)][string[]] $Arguments)
    Invoke-RequiredCommand -File 'kubectl' -Arguments $Arguments
}

try {
    Push-Location $repositoryRoot
    try {
        Invoke-RequiredCommand -File 'docker' -Arguments @('info')
        Invoke-RequiredCommand -File 'minikube' -Arguments @('start', '--driver=docker', '--cpus=4', '--memory=6g')
        Invoke-RequiredCommand -File 'minikube' -Arguments @('addons', 'enable', 'metrics-server')
        Invoke-RequiredCommand -File 'minikube' -Arguments @('addons', 'enable', 'ingress')
        Invoke-Kubectl -Arguments @('get', 'nodes')
        Invoke-Kubectl -Arguments @('get', 'pods', '--namespace=kube-system')

        foreach ($version in @('1.0.0', '1.1.0')) {
            $env:IMAGE_TAG = $version
            Invoke-RequiredCommand -File 'docker' -Arguments @('compose', 'build')

            foreach ($image in @('catalog-api', 'reservation-api', 'reviews-api', 'aggregator', 'api-gateway')) {
                Invoke-RequiredCommand -File 'minikube' -Arguments @(
                    'image', 'load', "hotel-reservation-system/${image}:${version}"
                )
            }
        }

        Invoke-Kubectl -Arguments @('apply', '-f', 'k8s/')
        Invoke-Kubectl -Arguments @(
            'wait', '--for=condition=available', 'deployment', '--all',
            "--namespace=$namespace", '--timeout=300s'
        )
        Invoke-Kubectl -Arguments @(
            'get', 'pods,deployments,replicasets,services', "--namespace=$namespace", '-o', 'wide'
        )

        $gatewayPod = (& kubectl get pods --namespace=$namespace -l app=hotel-api-gateway `
                -o 'jsonpath={.items[0].metadata.name}').Trim()
        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($gatewayPod)) {
            throw 'Could not find a ready API Gateway pod.'
        }
        Invoke-Kubectl -Arguments @(
            'exec', 'deployment/hotel-aggregator', '--', 'printenv', 'GrpcServices__CatalogService'
        )
        Invoke-Kubectl -Arguments @(
            'exec', 'deployment/hotel-catalog-api', '--', 'sh', '-c',
            'test -n "$POSTGRES_PASSWORD" && echo POSTGRES_PASSWORD=present'
        )
        Invoke-Kubectl -Arguments @(
            'exec', 'deployment/hotel-reviews-api', '--', 'sh', '-c',
            'test -n "$MONGO_ROOT_PASSWORD" && echo MONGO_ROOT_PASSWORD=present'
        )

        $gatewayUrl = "http://$(minikube ip):30080/"
        $gatewayPod = (& kubectl get pods --namespace=$namespace -l app=hotel-api-gateway `
                -o 'jsonpath={.items[0].metadata.name}').Trim()
        Invoke-Kubectl -Arguments @('delete', 'pod', $gatewayPod, "--namespace=$namespace", '--wait=true')
        Invoke-Kubectl -Arguments @(
            'rollout', 'status', 'deployment/hotel-api-gateway', "--namespace=$namespace", '--timeout=120s'
        )
        Invoke-Kubectl -Arguments @(
            'get', 'pods', '-l', 'app=hotel-api-gateway', "--namespace=$namespace", '-o', 'wide'
        )

        Invoke-Kubectl -Arguments @(
            'scale', 'deployment/hotel-api-gateway', '--replicas=3', "--namespace=$namespace"
        )
        Invoke-Kubectl -Arguments @(
            'rollout', 'status', 'deployment/hotel-api-gateway', "--namespace=$namespace", '--timeout=120s'
        )
        Invoke-Kubectl -Arguments @(
            'get', 'endpointslices', '-l', 'kubernetes.io/service-name=hotel-api-gateway',
            "--namespace=$namespace", '-o', 'wide'
        )
        foreach ($request in 1..15) {
            $response = Invoke-RestMethod -Uri $gatewayUrl -TimeoutSec 10
            Write-Output "Gateway request $request served by $($response.instance)"
        }

        $probeJob = Start-Job -ArgumentList $gatewayUrl -ScriptBlock {
            param($url)
            for ($request = 1; $request -le 40; $request++) {
                try {
                    $response = Invoke-RestMethod -Uri $url -TimeoutSec 3
                    "HTTP OK instance=$($response.instance)"
                }
                catch {
                    "HTTP FAILED: $($_.Exception.Message)"
                }
                Start-Sleep -Milliseconds 500
            }
        }
        Invoke-Kubectl -Arguments @(
            'set', 'image', 'deployment/hotel-api-gateway',
            'api-gateway=hotel-reservation-system/api-gateway:1.1.0', "--namespace=$namespace"
        )
        Invoke-Kubectl -Arguments @(
            'rollout', 'status', 'deployment/hotel-api-gateway', "--namespace=$namespace", '--timeout=180s'
        )
        Wait-Job -Job $probeJob -Timeout 60 | Out-Null
        $probeResults = @(Receive-Job -Job $probeJob)
        $probeResults
        Remove-Job -Job $probeJob
        if ($probeResults -match 'HTTP FAILED') {
            throw 'The gateway returned an error during the rolling update.'
        }
        Invoke-Kubectl -Arguments @(
            'rollout', 'history', 'deployment/hotel-api-gateway', "--namespace=$namespace"
        )
        Invoke-Kubectl -Arguments @(
            'rollout', 'undo', 'deployment/hotel-api-gateway', "--namespace=$namespace"
        )
        Invoke-Kubectl -Arguments @(
            'rollout', 'status', 'deployment/hotel-api-gateway', "--namespace=$namespace", '--timeout=180s'
        )

        Invoke-Kubectl -Arguments @(
            'set', 'image', 'deployment/hotel-catalog-api',
            'catalog-api=hotel-reservation-system/catalog-api:missing-lab-image', "--namespace=$namespace"
        )
        $previousErrorPreference = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        & kubectl rollout status deployment/hotel-catalog-api "--namespace=$namespace" --timeout=40s
        $invalidImageExitCode = $LASTEXITCODE
        $ErrorActionPreference = $previousErrorPreference
        if ($invalidImageExitCode -eq 0) {
            throw 'The intentionally invalid image unexpectedly rolled out successfully.'
        }

        Invoke-Kubectl -Arguments @(
            'get', 'pods', '-l', 'app=hotel-catalog-api', "--namespace=$namespace", '-o', 'wide'
        )
        Invoke-Kubectl -Arguments @(
            'describe', 'pods', '-l', 'app=hotel-catalog-api', "--namespace=$namespace"
        )
        Invoke-Kubectl -Arguments @(
            'get', 'events', "--namespace=$namespace", '--sort-by=.metadata.creationTimestamp'
        )

        Invoke-Kubectl -Arguments @(
            'set', 'image', 'deployment/hotel-catalog-api',
            'catalog-api=hotel-reservation-system/catalog-api:1.0.0', "--namespace=$namespace"
        )
        Invoke-Kubectl -Arguments @(
            'rollout', 'status', 'deployment/hotel-catalog-api', "--namespace=$namespace", '--timeout=180s'
        )
        Invoke-Kubectl -Arguments @(
            'logs', 'deployment/hotel-catalog-api', "--namespace=$namespace", '--tail=20'
        )
        Invoke-Kubectl -Arguments @(
            'exec', 'deployment/hotel-api-gateway', '--', 'printenv',
            'ReverseProxy__Clusters__catalog-cluster__Destinations__destination1__Address'
        )
        Invoke-Kubectl -Arguments @(
            'get', 'pods,deployments,replicasets,services', "--namespace=$namespace", '-o', 'wide'
        )
        Write-Output "The full output is saved in $reportPath"
    }
    finally {
        Pop-Location
    }
}
finally {
    $env:IMAGE_TAG = $originalImageTag
    Stop-Transcript | Out-Null
    $transcript = [System.IO.File]::ReadAllText($reportPath)
    $transcript = $transcript.Replace("`r`n", "`n")
    [System.IO.File]::WriteAllText($reportPath, $transcript, $utf8NoBom)
}
