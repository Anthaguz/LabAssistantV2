<#
.SYNOPSIS
    Guest OS orchestration script for a LabAssistant-deployed Active Directory lab.

.DESCRIPTION
    This script assumes LabAssistant has already provisioned the VMs and connected them
    to the expected Hyper-V virtual switches. The script validates the topology, configures
    guest networking deterministically by matching Hyper-V NIC MAC addresses to guest NICs,
    configures router/RRAS/NAT, builds the Contoso, Fabrikam, and Child domains, joins
    selected member servers to Contoso, and installs selected PKI-related Windows roles.

.NOTES
    This script is intended for isolated lab environments.
    It intentionally disables Windows Firewall and RDP NLA for lab convenience.
#>

param(
    [switch]$PlanOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

############################################################
# CONFIGURATION
############################################################

$LabConfig = @{
    Credentials = @{
        LocalUser                = "Administrator"
        LocalPassword            = "WIN1234"
        SafeModePassword         = "WIN1234!"
        ConvenienceAdminUser     = "Tony"
        ConvenienceAdminPassword = "WIN1234!"
    }

    Behavior = @{
        DisableFirewall = $true
        DisableRdpNla   = $true
    }

    Domains = @{
        Contoso = @{
            DomainName = "contoso.com"
            NetBIOS    = "CONTOSO"
            FirstDC    = "ContosoDC1"
            ReplicaDCs = @("ContosoDC2")
        }

        Fabrikam = @{
            DomainName = "fabrikam.com"
            NetBIOS    = "FABRIKAM"
            FirstDC    = "FabrikamDC1"
        }

        Child = @{
            DomainName       = "child.contoso.com"
            NetBIOS          = "CHILD"
            FirstDC          = "ChildDC1"
            ParentDomainName = "contoso.com"
            NewDomainName    = "child"
        }
    }

    Router = @{
        Name               = "Router"
        ExternalSwitchName = "Default Switch"
        NICs = @(
            @{ SwitchName = "Contoso";        IP = "10.0.0.1"; PrefixLength = 24; DNS = @("10.0.0.2", "10.0.0.3") },
            @{ SwitchName = "Fabrikam";       IP = "20.0.0.1"; PrefixLength = 24; DNS = @("20.0.0.2") },
            @{ SwitchName = "Child";          IP = "30.0.0.1"; PrefixLength = 24; DNS = @("30.0.0.2") },
            @{ SwitchName = "Default Switch" }
        )
    }

    VMs = @{
        ContosoDC1 = @{
            SwitchName   = "Contoso"
            IP           = "10.0.0.2"
            PrefixLength = 24
            Gateway      = "10.0.0.1"
            DNS          = @("10.0.0.2", "10.0.0.3")
        }

        ContosoDC2 = @{
            SwitchName   = "Contoso"
            IP           = "10.0.0.3"
            PrefixLength = 24
            Gateway      = "10.0.0.1"
            DNS          = @("10.0.0.2", "10.0.0.3")
        }

        ContSubCA = @{
            SwitchName   = "Contoso"
            IP           = "10.0.0.4"
            PrefixLength = 24
            Gateway      = "10.0.0.1"
            DNS          = @("10.0.0.2", "10.0.0.3")
            DomainJoin   = "contoso.com"
        }

        PKIOperations = @{
            SwitchName   = "Contoso"
            IP           = "10.0.0.5"
            PrefixLength = 24
            Gateway      = "10.0.0.1"
            DNS          = @("10.0.0.2", "10.0.0.3")
            DomainJoin   = "contoso.com"
        }

        SQL = @{
            SwitchName   = "Contoso"
            IP           = "10.0.0.6"
            PrefixLength = 24
            Gateway      = "10.0.0.1"
            DNS          = @("10.0.0.2", "10.0.0.3")
            DomainJoin   = "contoso.com"
        }

        RootCA = @{
            SwitchName   = "Contoso"
            IP           = "10.0.0.20"
            PrefixLength = 24
            Gateway      = "10.0.0.1"
            DNS          = @("10.0.0.2", "10.0.0.3")
            DomainJoin   = $null
        }

        FabrikamDC1 = @{
            SwitchName   = "Fabrikam"
            IP           = "20.0.0.2"
            PrefixLength = 24
            Gateway      = "20.0.0.1"
            DNS          = @("20.0.0.2")
        }

        ChildDC1 = @{
            SwitchName   = "Child"
            IP           = "30.0.0.2"
            PrefixLength = 24
            Gateway      = "30.0.0.1"
            DNS          = @("10.0.0.2")
        }
    }
}

############################################################
# LOGGING
############################################################

function Write-Stage {
    param([Parameter(Mandatory = $true)][string]$Message)
    Write-Host ""
    Write-Host "=== $Message ===" -ForegroundColor Cyan
}

function Write-Info {
    param([Parameter(Mandatory = $true)][string]$Message)
    Write-Host "[INFO] $Message" -ForegroundColor Green
}

function Write-Warn {
    param([Parameter(Mandatory = $true)][string]$Message)
    Write-Host "[WARN] $Message" -ForegroundColor Yellow
}

function Write-Fail {
    param([Parameter(Mandatory = $true)][string]$Message)
    Write-Host "[ERROR] $Message" -ForegroundColor Red
}

############################################################
# COMMON HELPERS
############################################################

function New-PlainTextCredential {
    param(
        [Parameter(Mandatory = $true)][string]$UserName,
        [Parameter(Mandatory = $true)][string]$Password
    )

    $securePassword = ConvertTo-SecureString $Password -AsPlainText -Force
    return New-Object System.Management.Automation.PSCredential -ArgumentList $UserName, $securePassword
}

function Get-LocalCredential {
    param([Parameter(Mandatory = $true)]$Config)

    return New-PlainTextCredential `
        -UserName $Config.Credentials.LocalUser `
        -Password $Config.Credentials.LocalPassword
}

function Get-DomainAdministratorCredential {
    param(
        [Parameter(Mandatory = $true)]$Config,
        [Parameter(Mandatory = $true)][string]$DomainName
    )

    return New-PlainTextCredential `
        -UserName "Administrator@$DomainName" `
        -Password $Config.Credentials.LocalPassword
}

function Get-ConvenienceDomainCredential {
    param(
        [Parameter(Mandatory = $true)]$Config,
        [Parameter(Mandatory = $true)][string]$DomainName
    )

    return New-PlainTextCredential `
        -UserName "$($Config.Credentials.ConvenienceAdminUser)@$DomainName" `
        -Password $Config.Credentials.ConvenienceAdminPassword
}

function Invoke-WithRetry {
    param(
        [Parameter(Mandatory = $true)][scriptblock]$Script,
        [int]$Retries = 60,
        [int]$DelaySeconds = 10,
        [string]$Activity = "operation"
    )

    $lastError = $null
    $operationTimer = [System.Diagnostics.Stopwatch]::StartNew()

    for ($attempt = 1; $attempt -le $Retries; $attempt++) {
        $attemptTimer = [System.Diagnostics.Stopwatch]::StartNew()

        try {
            $result = & $Script
            $attemptTimer.Stop()
            $operationTimer.Stop()
            Write-Info "$Activity succeeded on attempt $attempt after $([int]$attemptTimer.Elapsed.TotalSeconds)s. Total elapsed: $([int]$operationTimer.Elapsed.TotalSeconds)s."
            return $result
        }
        catch {
            $attemptTimer.Stop()
            $lastError = $_
            Write-Warn "$Activity failed on attempt $attempt of $Retries after $([int]$attemptTimer.Elapsed.TotalSeconds)s. $($_.Exception.Message)"
            Start-Sleep -Seconds $DelaySeconds
        }
    }

    $operationTimer.Stop()
    throw "$Activity failed after $Retries attempts. Last error: $($lastError.Exception.Message)"
}

function Wait-ForPowerShellDirect {
    param(
        [Parameter(Mandatory = $true)][string]$VMName,
        [Parameter(Mandatory = $true)][PSCredential]$Credential,
        [int]$Retries = 60,
        [int]$DelaySeconds = 10
    )

    Invoke-WithRetry `
        -Activity "Waiting for PowerShell Direct on $VMName" `
        -Retries $Retries `
        -DelaySeconds $DelaySeconds `
        -Script {
            Invoke-Command -VMName $VMName -Credential $Credential -ScriptBlock {
                "ready"
            } -ErrorAction Stop | Out-Null
        }

    Write-Info "$VMName is reachable through PowerShell Direct."
}

function Test-CanUsePowerShellDirect {
    param(
        [Parameter(Mandatory = $true)][string]$VMName,
        [Parameter(Mandatory = $true)][PSCredential]$Credential
    )

    try {
        Invoke-Command -VMName $VMName -Credential $Credential -ScriptBlock {
            "ready"
        } -ErrorAction Stop | Out-Null

        return $true
    }
    catch {
        return $false
    }
}

function Test-IsExpectedRestartDisconnect {
    param([Parameter(Mandatory = $true)]$ErrorRecord)

    $message = $ErrorRecord.Exception.Message

    if ([string]::IsNullOrWhiteSpace($message)) {
        return $false
    }

    return ($message -match 'reboot|restart|shut\s*down|connection.*(closed|lost|terminated)|client cannot connect|WSMan|WinRM|RPC server is unavailable|I/O operation has been aborted')
}

function Write-ExpectedRestartWarning {
    param(
        [Parameter(Mandatory = $true)][string]$Activity,
        [Parameter(Mandatory = $true)]$ErrorRecord
    )

    if (-not (Test-IsExpectedRestartDisconnect -ErrorRecord $ErrorRecord)) {
        throw "$Activity failed before the expected restart boundary. $($ErrorRecord.Exception.Message)"
    }

    Write-Warn "$Activity ended while the VM was restarting: $($ErrorRecord.Exception.Message)"
    Write-Warn "Readiness validation will confirm whether the operation completed successfully."
}

function Convert-ToGuestMacAddress {
    param([Parameter(Mandatory = $true)][string]$MacAddress)

    $clean = ($MacAddress -replace '[-:\s]', '').ToUpperInvariant()

    if ($clean.Length -ne 12) {
        throw "Invalid MAC address format: $MacAddress"
    }

    return ($clean -replace '(.{2})(?!$)', '$1-')
}

function Get-VMNicMacBySwitch {
    param(
        [Parameter(Mandatory = $true)][string]$VMName,
        [Parameter(Mandatory = $true)][string]$SwitchName
    )

    $matchingNics = @(Get-VMNetworkAdapter -VMName $VMName -ErrorAction Stop | Where-Object {
        $_.SwitchName -eq $SwitchName
    })

    if ($matchingNics.Count -eq 0) {
        throw "VM '$VMName' does not have a network adapter attached to switch '$SwitchName'."
    }

    if ($matchingNics.Count -gt 1) {
        throw "VM '$VMName' has more than one network adapter attached to switch '$SwitchName'. This script requires deterministic mapping."
    }

    if ([string]::IsNullOrWhiteSpace($matchingNics[0].MacAddress)) {
        throw "VM '$VMName' adapter on switch '$SwitchName' does not have a MAC address."
    }

    return Convert-ToGuestMacAddress -MacAddress $matchingNics[0].MacAddress
}

############################################################
# PREFLIGHT VALIDATION
############################################################

function Test-LabTopology {
    param(
        [Parameter(Mandatory = $true)]$Config,
        [Parameter(Mandatory = $true)][hashtable]$CredentialMap
    )

    Write-Stage "PHASE 0 - PREFLIGHT VALIDATION"

    $expectedVmNames = @($Config.VMs.Keys) + @($Config.Router.Name)
    $expectedSwitchNames = @()

    foreach ($vmName in $Config.VMs.Keys) {
        $expectedSwitchNames += $Config.VMs[$vmName].SwitchName
    }

    foreach ($nic in $Config.Router.NICs) {
        $expectedSwitchNames += $nic.SwitchName
    }

    $expectedSwitchNames = $expectedSwitchNames | Sort-Object -Unique

    foreach ($switchName in $expectedSwitchNames) {
        Get-VMSwitch -Name $switchName -ErrorAction Stop | Out-Null
        Write-Info "Validated virtual switch: $switchName"
    }

    foreach ($vmName in $expectedVmNames) {
        $vm = Get-VM -Name $vmName -ErrorAction Stop

        if ($vm.State -ne "Running") {
            throw "VM '$vmName' must be running before lab configuration starts. Current state: $($vm.State)."
        }

        Write-Info "Validated VM exists: $vmName"
    }

    foreach ($vmName in $expectedVmNames) {
        $credentialsToTry = if ($CredentialMap.ContainsKey($vmName)) { @($CredentialMap[$vmName]) } else { @() }
        $reachable = $false
        $lastError = $null

        foreach ($candidateCredential in $credentialsToTry) {
            try {
                Wait-ForPowerShellDirect -VMName $vmName -Credential $candidateCredential -Retries 1 -DelaySeconds 1
                $reachable = $true
                break
            }
            catch {
                $lastError = $_
                Write-Warn "Preflight PowerShell Direct check failed on $vmName for $($candidateCredential.UserName): $($_.Exception.Message)"
            }
        }

        if (-not $reachable) {
            throw "VM '$vmName' is not reachable through PowerShell Direct with any expected credential. Last error: $($lastError.Exception.Message)"
        }
    }

    $macOwners = @{}

    foreach ($vmName in $Config.VMs.Keys) {
        $switchName = $Config.VMs[$vmName].SwitchName
        $mac = Get-VMNicMacBySwitch -VMName $vmName -SwitchName $switchName

        if ($macOwners.ContainsKey($mac)) {
            throw "Duplicate MAC address detected: $mac is used by '$($macOwners[$mac])' and '$vmName'."
        }

        $macOwners[$mac] = $vmName
        Write-Info "Validated NIC mapping: $vmName -> $switchName -> $mac"
    }

    foreach ($nic in $Config.Router.NICs) {
        $mac = Get-VMNicMacBySwitch -VMName $Config.Router.Name -SwitchName $nic.SwitchName

        if ($macOwners.ContainsKey($mac)) {
            throw "Duplicate MAC address detected: $mac is used by '$($macOwners[$mac])' and '$($Config.Router.Name):$($nic.SwitchName)'."
        }

        $macOwners[$mac] = "$($Config.Router.Name):$($nic.SwitchName)"
        Write-Info "Validated router NIC mapping: $($Config.Router.Name) -> $($nic.SwitchName) -> $mac"
    }

    Write-Info "Preflight validation completed successfully."
}

############################################################
# GUEST NETWORK CONFIGURATION
############################################################

function Initialize-VMNetworkBySwitch {
    param(
        [Parameter(Mandatory = $true)][string]$VMName,
        [Parameter(Mandatory = $true)][string]$SwitchName,
        [Parameter(Mandatory = $true)][string]$IPAddress,
        [Parameter(Mandatory = $true)][int]$PrefixLength,
        [Parameter(Mandatory = $true)][string]$DefaultGateway,
        [Parameter(Mandatory = $true)][string[]]$DnsServers,
        [Parameter(Mandatory = $true)][string]$ComputerName,
        [Parameter(Mandatory = $true)][PSCredential]$Credential
    )

    $guestMac = Get-VMNicMacBySwitch -VMName $VMName -SwitchName $SwitchName
    $restartExpected = $false

    Write-Info "Configuring $VMName network adapter on switch '$SwitchName' using MAC $guestMac."

    try {
        $result = Invoke-Command -VMName $VMName -Credential $Credential -ScriptBlock {
            param(
                [string]$MacAddress,
                [string]$DesiredIPAddress,
                [int]$DesiredPrefixLength,
                [string]$DesiredGateway,
                [string[]]$DesiredDnsServers,
                [string]$DesiredComputerName
            )

            $adapter = Get-NetAdapter | Where-Object { $_.MacAddress -eq $MacAddress } | Select-Object -First 1

            if (-not $adapter) {
                throw "No guest adapter found with MAC address $MacAddress."
            }

            $interfaceIndex = $adapter.ifIndex

            $existingAddresses = @(Get-NetIPAddress -InterfaceIndex $interfaceIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue | Where-Object {
                $_.IPAddress -notlike '169.254.*'
            })

            foreach ($address in $existingAddresses) {
                if (($address.IPAddress -ne $DesiredIPAddress) -or ($address.PrefixLength -ne $DesiredPrefixLength)) {
                    Remove-NetIPAddress `
                        -InterfaceIndex $interfaceIndex `
                        -IPAddress $address.IPAddress `
                        -Confirm:$false `
                        -ErrorAction SilentlyContinue
                }
            }

            $desiredAddressExists = @(Get-NetIPAddress -InterfaceIndex $interfaceIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue | Where-Object {
                ($_.IPAddress -eq $DesiredIPAddress) -and ($_.PrefixLength -eq $DesiredPrefixLength)
            }).Count -gt 0

            if (-not $desiredAddressExists) {
                New-NetIPAddress `
                    -InterfaceIndex $interfaceIndex `
                    -IPAddress $DesiredIPAddress `
                    -PrefixLength $DesiredPrefixLength `
                    -ErrorAction Stop | Out-Null
            }

            if (-not [string]::IsNullOrWhiteSpace($DesiredGateway)) {
                Get-NetRoute -InterfaceIndex $interfaceIndex -DestinationPrefix "0.0.0.0/0" -ErrorAction SilentlyContinue | Where-Object {
                    $_.NextHop -ne $DesiredGateway
                } | Remove-NetRoute -Confirm:$false -ErrorAction SilentlyContinue

                $gatewayRouteExists = @(Get-NetRoute -InterfaceIndex $interfaceIndex -DestinationPrefix "0.0.0.0/0" -ErrorAction SilentlyContinue | Where-Object {
                    $_.NextHop -eq $DesiredGateway
                }).Count -gt 0

                if (-not $gatewayRouteExists) {
                    New-NetRoute `
                        -InterfaceIndex $interfaceIndex `
                        -DestinationPrefix "0.0.0.0/0" `
                        -NextHop $DesiredGateway `
                        -ErrorAction Stop | Out-Null
                }
            }

            Set-DnsClientServerAddress `
                -InterfaceIndex $interfaceIndex `
                -ServerAddresses $DesiredDnsServers `
                -ErrorAction Stop

            $currentComputerName = $env:COMPUTERNAME
            $renameRequired = $currentComputerName -ine $DesiredComputerName

            if ($renameRequired) {
                Rename-Computer -NewName $DesiredComputerName -Force -ErrorAction Stop

                [pscustomobject]@{
                    RestartRequired = $true
                    PreviousName    = $currentComputerName
                    DesiredName     = $DesiredComputerName
                    AdapterName     = $adapter.Name
                }

                Restart-Computer -Force
            }
            else {
                [pscustomobject]@{
                    RestartRequired = $false
                    PreviousName    = $currentComputerName
                    DesiredName     = $DesiredComputerName
                    AdapterName     = $adapter.Name
                }
            }
        } -ArgumentList $guestMac, $IPAddress, $PrefixLength, $DefaultGateway, (, $DnsServers), $ComputerName -ErrorAction Stop

        $lastResult = @($result) | Select-Object -Last 1

        if ($lastResult -and ($lastResult.PSObject.Properties.Name -contains "RestartRequired")) {
            $restartExpected = [bool]$lastResult.RestartRequired
        }
    }
    catch {
        $restartExpected = $true
        Write-Warn "Network initialization command for $VMName ended with: $($_.Exception.Message)"
        Write-Warn "This can be expected when the VM restarts during rename. The script will wait and validate the VM state."
    }

    if ($restartExpected) {
        Write-Info "$VMName is expected to restart. Waiting before validation."
        Start-Sleep -Seconds 30
    }
    else {
        Start-Sleep -Seconds 5
    }

    Wait-ForPowerShellDirect -VMName $VMName -Credential $Credential -Retries 90 -DelaySeconds 10

    Invoke-WithRetry `
        -Activity "Validating network state for $VMName" `
        -Retries 30 `
        -DelaySeconds 10 `
        -Script {
            Assert-VMNetworkState `
                -VMName $VMName `
                -SwitchName $SwitchName `
                -IPAddress $IPAddress `
                -PrefixLength $PrefixLength `
                -DnsServers $DnsServers `
                -ComputerName $ComputerName `
                -Credential $Credential
        }
}

function Assert-VMNetworkState {
    param(
        [Parameter(Mandatory = $true)][string]$VMName,
        [Parameter(Mandatory = $true)][string]$SwitchName,
        [Parameter(Mandatory = $true)][string]$IPAddress,
        [Parameter(Mandatory = $true)][int]$PrefixLength,
        [Parameter(Mandatory = $true)][string[]]$DnsServers,
        [Parameter(Mandatory = $true)][string]$ComputerName,
        [Parameter(Mandatory = $true)][PSCredential]$Credential
    )

    $guestMac = Get-VMNicMacBySwitch -VMName $VMName -SwitchName $SwitchName

    Invoke-Command -VMName $VMName -Credential $Credential -ScriptBlock {
        param(
            [string]$MacAddress,
            [string]$ExpectedIPAddress,
            [int]$ExpectedPrefixLength,
            [string[]]$ExpectedDnsServers,
            [string]$ExpectedComputerName
        )

        if ($env:COMPUTERNAME -ine $ExpectedComputerName) {
            throw "Expected computer name '$ExpectedComputerName', but current name is '$env:COMPUTERNAME'."
        }

        $adapter = Get-NetAdapter | Where-Object { $_.MacAddress -eq $MacAddress } | Select-Object -First 1

        if (-not $adapter) {
            throw "No guest adapter found with MAC address $MacAddress."
        }

        $address = Get-NetIPAddress -InterfaceIndex $adapter.ifIndex -AddressFamily IPv4 -ErrorAction Stop | Where-Object {
            ($_.IPAddress -eq $ExpectedIPAddress) -and ($_.PrefixLength -eq $ExpectedPrefixLength)
        }

        if (-not $address) {
            throw "Expected IP address $ExpectedIPAddress/$ExpectedPrefixLength was not found on adapter $($adapter.Name)."
        }

        $actualDnsServers = @(Get-DnsClientServerAddress -InterfaceIndex $adapter.ifIndex -AddressFamily IPv4).ServerAddresses

        $expectedDnsList = @((@($ExpectedDnsServers) -join ',') -split '[, ]+' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
        $actualDnsList   = @((@($actualDnsServers) -join ',') -split '[, ]+' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })

        $expectedDnsText = $expectedDnsList -join ','
        $actualDnsText   = $actualDnsList -join ','

        if ($expectedDnsText -ne $actualDnsText) {
            throw "Expected DNS servers '$expectedDnsText', but found '$actualDnsText'."
        }
    } -ArgumentList $guestMac, $IPAddress, $PrefixLength, (, $DnsServers), $ComputerName -ErrorAction Stop

    Write-Info "Validated network state for $VMName."
}

function Initialize-RouterNetwork {
    param(
        [Parameter(Mandatory = $true)]$Config,
        [Parameter(Mandatory = $true)][PSCredential]$Credential
    )

    $routerName = $Config.Router.Name
    $mappedNics = @()

    foreach ($nic in $Config.Router.NICs) {
        $hasStaticIP = $nic.ContainsKey("IP") -and -not [string]::IsNullOrWhiteSpace($nic.IP)

        $mappedNics += @{
            SwitchName   = $nic.SwitchName
            MacAddress   = Get-VMNicMacBySwitch -VMName $routerName -SwitchName $nic.SwitchName
            HasStaticIP  = $hasStaticIP
            IP           = if ($hasStaticIP) { $nic.IP } else { $null }
            PrefixLength = if ($nic.ContainsKey("PrefixLength") -and $nic.PrefixLength) { [int]$nic.PrefixLength } else { 24 }
            DNS          = if ($nic.ContainsKey("DNS")) { @($nic.DNS) } else { @() }
        }
    }

    $mappedNicsJson = $mappedNics | ConvertTo-Json -Depth 5

    Write-Info "Configuring router network adapters."

    Invoke-Command -VMName $routerName -Credential $Credential -ScriptBlock {
        param([string]$NicMapJson)

        $nicMap = $NicMapJson | ConvertFrom-Json

        foreach ($entry in $nicMap) {
            $adapter = Get-NetAdapter | Where-Object { $_.MacAddress -eq $entry.MacAddress } | Select-Object -First 1

            if (-not $adapter) {
                throw "No router guest adapter found with MAC address $($entry.MacAddress) for switch $($entry.SwitchName)."
            }

            $interfaceIndex = $adapter.ifIndex

            if ($entry.HasStaticIP) {
                $existingAddresses = @(Get-NetIPAddress -InterfaceIndex $interfaceIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue | Where-Object {
                    $_.IPAddress -notlike '169.254.*'
                })

                foreach ($address in $existingAddresses) {
                    if (($address.IPAddress -ne $entry.IP) -or ($address.PrefixLength -ne [int]$entry.PrefixLength)) {
                        Remove-NetIPAddress `
                            -InterfaceIndex $interfaceIndex `
                            -IPAddress $address.IPAddress `
                            -Confirm:$false `
                            -ErrorAction SilentlyContinue
                    }
                }

                $desiredAddressExists = @(Get-NetIPAddress -InterfaceIndex $interfaceIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue | Where-Object {
                    ($_.IPAddress -eq $entry.IP) -and ($_.PrefixLength -eq [int]$entry.PrefixLength)
                }).Count -gt 0

                if (-not $desiredAddressExists) {
                    New-NetIPAddress `
                        -InterfaceIndex $interfaceIndex `
                        -IPAddress $entry.IP `
                        -PrefixLength ([int]$entry.PrefixLength) `
                        -ErrorAction Stop | Out-Null
                }

                if ($entry.DNS) {
                    Set-DnsClientServerAddress `
                        -InterfaceIndex $interfaceIndex `
                        -ServerAddresses @($entry.DNS) `
                        -ErrorAction Stop
                }
            }
            else {
                Get-NetRoute -InterfaceIndex $interfaceIndex -DestinationPrefix "0.0.0.0/0" -ErrorAction SilentlyContinue | Where-Object {
                    $_.Protocol -ne "Dhcp"
                } | Remove-NetRoute -Confirm:$false -ErrorAction SilentlyContinue

                Get-NetIPAddress -InterfaceIndex $interfaceIndex -AddressFamily IPv4 -ErrorAction SilentlyContinue | Where-Object {
                    ($_.IPAddress -notlike '169.254.*') -and ($_.PrefixOrigin -ne "Dhcp")
                } | Remove-NetIPAddress -Confirm:$false -ErrorAction SilentlyContinue

                Set-NetIPInterface `
                    -InterfaceIndex $interfaceIndex `
                    -Dhcp Enabled `
                    -AddressFamily IPv4 `
                    -ErrorAction SilentlyContinue

                Set-DnsClientServerAddress `
                    -InterfaceIndex $interfaceIndex `
                    -ResetServerAddresses `
                    -ErrorAction SilentlyContinue
            }

            Write-Output "Configured router adapter '$($adapter.Name)' for switch '$($entry.SwitchName)'."
        }
    } -ArgumentList $mappedNicsJson -ErrorAction Stop

    Write-Info "Router network adapters configured."
}

############################################################
# BASE REMOTE ACCESS AND ROUTER SERVICES
############################################################

function Enable-BaseRemoteAccess {
    param(
        [Parameter(Mandatory = $true)][string[]]$VMNames,
        [Parameter(Mandatory = $true)][PSCredential]$Credential,
        [bool]$DisableFirewall,
        [bool]$DisableRdpNla
    )

    Write-Info "Configuring base remote access settings on VMs."

    $jobs = foreach ($vmName in $VMNames) {
        Invoke-Command -VMName $vmName -Credential $Credential -AsJob -ScriptBlock {
            param([bool]$ShouldDisableFirewall, [bool]$ShouldDisableNla)

            $server = hostname

            if ($ShouldDisableFirewall) {
                netsh advfirewall set allprofiles state off | Out-Null
            }

            Set-ItemProperty `
                -Path 'HKLM:\System\CurrentControlSet\Control\Terminal Server' `
                -Name "fDenyTSConnections" `
                -Value 0 `
                -ErrorAction Stop

            if ($ShouldDisableNla) {
                try {
                    (Get-WmiObject `
                        -Class "Win32_TSGeneralSetting" `
                        -Namespace root\cimv2\terminalservices `
                        -ComputerName $server `
                        -Filter "TerminalName='RDP-tcp'").SetUserAuthenticationRequired(0) | Out-Null
                }
                catch {
                    Write-Output "NLA setting could not be changed on $server. $($_.Exception.Message)"
                }
            }

            try {
                Get-NetConnectionProfile | ForEach-Object {
                    Set-NetConnectionProfile `
                        -InterfaceIndex $_.InterfaceIndex `
                        -NetworkCategory Private `
                        -ErrorAction SilentlyContinue
                }
            }
            catch {
                Write-Output "Network profile update could not be completed on $server. $($_.Exception.Message)"
            }

            Write-Output "Base remote access configured on $server."
        } -ArgumentList $DisableFirewall, $DisableRdpNla
    }

    $jobs | Wait-Job | Out-Null

    foreach ($job in $jobs) {
        Receive-Job $job -ErrorAction Stop
    }

    $jobs | Remove-Job

    Write-Info "Base remote access configuration completed."
}

function Ensure-RouterRole {
    param(
        [Parameter(Mandatory = $true)][string]$VMName,
        [Parameter(Mandatory = $true)][PSCredential]$Credential
    )

    Write-Info "Ensuring RemoteAccess and Routing roles are installed on $VMName."

    Invoke-Command -VMName $VMName -Credential $Credential -ScriptBlock {
        $featureNames = @("RemoteAccess", "Routing")
        $features = Get-WindowsFeature -Name $featureNames
        $missing = @($features | Where-Object { $_.InstallState -ne "Installed" } | Select-Object -ExpandProperty Name)

        if ($missing.Count -gt 0) {
            Install-WindowsFeature -Name $missing -IncludeManagementTools -ErrorAction Stop | Out-Null
        }

        Write-Output "Router roles are installed."
    } -ErrorAction Stop
}

function Enable-VMRouting {
    param(
        [Parameter(Mandatory = $true)][string]$VMName,
        [Parameter(Mandatory = $true)][PSCredential]$Credential,
        [Parameter(Mandatory = $true)][string]$ExternalSwitchName
    )

    $externalMac = Get-VMNicMacBySwitch -VMName $VMName -SwitchName $ExternalSwitchName

    Write-Info "Enabling RRAS routing on $VMName."

    Invoke-Command -VMName $VMName -Credential $Credential -ScriptBlock {
        param([string]$ExternalMacAddress)

        Import-Module RemoteAccess -ErrorAction SilentlyContinue

        try {
            Install-RemoteAccess -VpnType RoutingOnly -ErrorAction Stop
        }
        catch {
            if ($_.Exception.Message -notmatch 'already|configured|installed') {
                throw
            }
        }

        Get-NetAdapter | Where-Object { $_.Status -eq "Up" } | ForEach-Object {
            Set-NetIPInterface `
                -InterfaceIndex $_.ifIndex `
                -Forwarding Enabled `
                -AddressFamily IPv4 `
                -ErrorAction Stop
        }

        $externalAdapter = Get-NetAdapter | Where-Object { $_.MacAddress -eq $ExternalMacAddress } | Select-Object -First 1

        if (-not $externalAdapter) {
            throw "External adapter with MAC $ExternalMacAddress was not found in the router guest OS."
        }

        Write-Output "Routing enabled. External adapter: $($externalAdapter.Name)."
    } -ArgumentList $externalMac -ErrorAction Stop
}

function Enable-VMNat {
    param(
        [Parameter(Mandatory = $true)]$Config,
        [Parameter(Mandatory = $true)][PSCredential]$Credential
    )

    $routerName = $Config.Router.Name
    $externalSwitchName = $Config.Router.ExternalSwitchName
    $externalMac = Get-VMNicMacBySwitch -VMName $routerName -SwitchName $externalSwitchName

    $internalMacs = @()

    foreach ($nic in $Config.Router.NICs) {
        if ($nic.SwitchName -ne $externalSwitchName) {
            $internalMacs += Get-VMNicMacBySwitch -VMName $routerName -SwitchName $nic.SwitchName
        }
    }

    $internalMacsJson = ConvertTo-Json -InputObject $internalMacs -Compress

    Write-Info "Configuring RRAS NAT on $routerName."

    Invoke-Command -VMName $routerName -Credential $Credential -ScriptBlock {
        param(
            [string]$ExternalMacAddress,
            [string]$InternalMacsJson
        )

        $internalMacAddresses = [System.Collections.Generic.List[string]]::new()

        foreach ($mac in ($InternalMacsJson | ConvertFrom-Json)) {
            $internalMacAddresses.Add([string]$mac)
        }

        function Invoke-NetshNatCommand {
            param(
                [Parameter(Mandatory = $true)][string[]]$Arguments,
                [switch]$IgnoreMissing
            )

            $output = & netsh @Arguments 2>&1
            $exitCode = $LASTEXITCODE
            $outputText = @($output) -join "`n"
            $commandText = "netsh $($Arguments -join ' ')"

            if ($exitCode -eq 0) {
                return
            }

            if ($IgnoreMissing -and ($outputText -match 'not found|does not exist|not configured|not installed')) {
                return
            }

            throw "$commandText failed with exit code $exitCode. Output: $outputText"
        }

        $externalAdapter = Get-NetAdapter | Where-Object { $_.MacAddress -eq $ExternalMacAddress } | Select-Object -First 1

        if (-not $externalAdapter) {
            throw "External NAT adapter with MAC $ExternalMacAddress was not found."
        }

        $internalAdapters = @()

        foreach ($mac in $internalMacAddresses) {
            $adapter = Get-NetAdapter | Where-Object { $_.MacAddress -eq $mac } | Select-Object -First 1

            if (-not $adapter) {
                throw "Internal NAT adapter with MAC $mac was not found."
            }

            $internalAdapters += $adapter
        }

        Invoke-NetshNatCommand -Arguments @("routing", "ip", "nat", "install")

        Invoke-NetshNatCommand -Arguments @("routing", "ip", "nat", "delete", "interface", $externalAdapter.Name) -IgnoreMissing
        Invoke-NetshNatCommand -Arguments @("routing", "ip", "nat", "add", "interface", $externalAdapter.Name, "mode=full")

        foreach ($adapter in $internalAdapters) {
            Invoke-NetshNatCommand -Arguments @("routing", "ip", "nat", "delete", "interface", $adapter.Name) -IgnoreMissing
            Invoke-NetshNatCommand -Arguments @("routing", "ip", "nat", "add", "interface", $adapter.Name, "mode=private")
        }

        Write-Output "NAT configured. External adapter: $($externalAdapter.Name). Internal adapters: $($internalAdapters.Name -join ', ')."
    } -ArgumentList $externalMac, $internalMacsJson -ErrorAction Stop
}

############################################################
# WINDOWS FEATURE INSTALLATION
############################################################

function Ensure-WindowsFeatures {
    param(
        [Parameter(Mandatory = $true)][string]$VMName,
        [Parameter(Mandatory = $true)][PSCredential]$Credential,
        [Parameter(Mandatory = $true)][string[]]$FeatureNames,
        [switch]$IncludeManagementTools
    )

    Write-Info "Ensuring Windows features are installed on ${VMName}: $($FeatureNames -join ', ')"

    Invoke-Command -VMName $VMName -Credential $Credential -ScriptBlock {
        param([string[]]$Names, [bool]$WithManagementTools)

        $features = Get-WindowsFeature -Name $Names
        $missing = @($features | Where-Object { $_.InstallState -ne "Installed" } | Select-Object -ExpandProperty Name)

        if ($missing.Count -eq 0) {
            Write-Output "Requested features are already installed."
            return
        }

        if ($WithManagementTools) {
            Install-WindowsFeature -Name $missing -IncludeManagementTools -ErrorAction Stop | Out-Null
        }
        else {
            Install-WindowsFeature -Name $missing -ErrorAction Stop | Out-Null
        }

        Write-Output "Installed features: $($missing -join ', ')"
    } -ArgumentList (, $FeatureNames), ([bool]$IncludeManagementTools) -ErrorAction Stop
}

############################################################
# ACTIVE DIRECTORY HELPERS
############################################################

function Test-IsDomainControllerForDomain {
    param(
        [Parameter(Mandatory = $true)][string]$VMName,
        [Parameter(Mandatory = $true)][PSCredential]$Credential,
        [Parameter(Mandatory = $true)][string]$DomainName
    )

    try {
        $result = Invoke-Command -VMName $VMName -Credential $Credential -ScriptBlock {
            param([string]$ExpectedDomainName)

            $computerSystem = Get-CimInstance Win32_ComputerSystem

            if ($computerSystem.DomainRole -notin 4, 5) {
                return $false
            }

            Import-Module ActiveDirectory -ErrorAction Stop
            $domain = Get-ADDomain -ErrorAction Stop

            return ($domain.DNSRoot -ieq $ExpectedDomainName)
        } -ArgumentList $DomainName -ErrorAction Stop

        return [bool]$result
    }
    catch {
        return $false
    }
}

function Wait-ForDomainReady {
    param(
        [Parameter(Mandatory = $true)][string]$VMName,
        [Parameter(Mandatory = $true)][PSCredential]$Credential,
        [Parameter(Mandatory = $true)][string]$DomainName,
        [int]$Retries = 90,
        [int]$DelaySeconds = 10
    )

    Write-Info "Waiting for domain readiness: $DomainName on $VMName."

    Invoke-WithRetry `
        -Activity "Waiting for domain $DomainName" `
        -Retries $Retries `
        -DelaySeconds $DelaySeconds `
        -Script {
            Invoke-Command -VMName $VMName -Credential $Credential -ScriptBlock {
                param([string]$ExpectedDomainName)

                Resolve-DnsName $ExpectedDomainName -ErrorAction Stop | Out-Null
                Resolve-DnsName "_ldap._tcp.dc._msdcs.$ExpectedDomainName" -Type SRV -ErrorAction Stop | Out-Null

                Import-Module ActiveDirectory -ErrorAction Stop
                $domain = Get-ADDomain -ErrorAction Stop

                if ($domain.DNSRoot -ine $ExpectedDomainName) {
                    throw "Expected domain '$ExpectedDomainName', but local domain is '$($domain.DNSRoot)'."
                }
            } -ArgumentList $DomainName -ErrorAction Stop
        }

    Write-Info "Domain is ready: $DomainName."
}

function Wait-ForDomainDnsReady {
    param(
        [Parameter(Mandatory = $true)][string]$VMName,
        [Parameter(Mandatory = $true)][PSCredential]$Credential,
        [Parameter(Mandatory = $true)][string]$DomainName,
        [int]$Retries = 30,
        [int]$DelaySeconds = 5
    )

    Write-Info "Waiting for DNS readiness for domain $DomainName from $VMName."

    Invoke-WithRetry `
        -Activity "Waiting for DNS records for $DomainName from $VMName" `
        -Retries $Retries `
        -DelaySeconds $DelaySeconds `
        -Script {
            Invoke-Command -VMName $VMName -Credential $Credential -ScriptBlock {
                param([string]$ExpectedDomainName)

                $dnsServers = @(Get-DnsClientServerAddress -AddressFamily IPv4 | Select-Object -ExpandProperty ServerAddresses)
                Write-Output "Current DNS servers on $env:COMPUTERNAME: $($dnsServers -join ', ')"

                Resolve-DnsName $ExpectedDomainName -QuickTimeout -ErrorAction Stop | Out-Null
                Resolve-DnsName "_ldap._tcp.dc._msdcs.$ExpectedDomainName" -Type SRV -QuickTimeout -ErrorAction Stop | Out-Null
            } -ArgumentList $DomainName -ErrorAction Stop
        }

    Write-Info "DNS records for $DomainName are resolvable from $VMName."
}

function Ensure-NewForest {
    param(
        [Parameter(Mandatory = $true)][string]$VMName,
        [Parameter(Mandatory = $true)][string]$DomainName,
        [Parameter(Mandatory = $true)][string]$NetBIOSName,
        [Parameter(Mandatory = $true)][PSCredential]$LocalCredential,
        [Parameter(Mandatory = $true)][PSCredential]$DomainAdministratorCredential,
        [Parameter(Mandatory = $true)][string]$SafeModePassword
    )

    if (Test-CanUsePowerShellDirect -VMName $VMName -Credential $LocalCredential) {
        if (Test-IsDomainControllerForDomain -VMName $VMName -Credential $LocalCredential -DomainName $DomainName) {
            Write-Info "$VMName is already a domain controller for $DomainName. Skipping forest creation."
            return
        }
    }
    elseif (Test-IsDomainControllerForDomain -VMName $VMName -Credential $DomainAdministratorCredential -DomainName $DomainName) {
        Write-Info "$VMName is already a domain controller for $DomainName. Skipping forest creation."
        return
    }

    Write-Info "Creating new forest '$DomainName' on $VMName."

    try {
        Invoke-Command -VMName $VMName -Credential $LocalCredential -ScriptBlock {
            param(
                [string]$Domain,
                [string]$NetBIOS,
                [string]$DsrmPassword
            )

            Import-Module ADDSDeployment -ErrorAction Stop

            $secureDsrmPassword = ConvertTo-SecureString $DsrmPassword -AsPlainText -Force

            Install-ADDSForest `
                -CreateDnsDelegation:$false `
                -DatabasePath "C:\Windows\NTDS" `
                -DomainMode "WinThreshold" `
                -DomainName $Domain `
                -DomainNetbiosName $NetBIOS `
                -ForestMode "WinThreshold" `
                -InstallDns:$true `
                -LogPath "C:\Windows\NTDS" `
                -NoRebootOnCompletion:$false `
                -SysvolPath "C:\Windows\SYSVOL" `
                -SafeModeAdministratorPassword $secureDsrmPassword `
                -Force:$true
        } -ArgumentList $DomainName, $NetBIOSName, $SafeModePassword -ErrorAction Stop
    }
    catch {
        Write-ExpectedRestartWarning -Activity "Forest creation command for $VMName" -ErrorRecord $_
    }

    Wait-ForPowerShellDirect -VMName $VMName -Credential $DomainAdministratorCredential -Retries 90
    Wait-ForDomainReady -VMName $VMName -Credential $DomainAdministratorCredential -DomainName $DomainName
}

function Ensure-ReplicaDomainController {
    param(
        [Parameter(Mandatory = $true)][string]$VMName,
        [Parameter(Mandatory = $true)][string]$DomainName,
        [Parameter(Mandatory = $true)][PSCredential]$LocalCredential,
        [Parameter(Mandatory = $true)][PSCredential]$DomainAdministratorCredential,
        [Parameter(Mandatory = $true)][string]$SafeModePassword
    )

    if (Test-IsDomainControllerForDomain -VMName $VMName -Credential $DomainAdministratorCredential -DomainName $DomainName) {
        Write-Info "$VMName is already a domain controller for $DomainName. Skipping replica promotion."
        return
    }

    Write-Info "Promoting $VMName as replica domain controller for $DomainName."

    try {
        Invoke-Command -VMName $VMName -Credential $LocalCredential -ScriptBlock {
            param(
                [string]$Domain,
                [PSCredential]$DomainCredential,
                [string]$DsrmPassword
            )

            Import-Module ADDSDeployment -ErrorAction Stop

            $secureDsrmPassword = ConvertTo-SecureString $DsrmPassword -AsPlainText -Force

            Install-ADDSDomainController `
                -CreateDnsDelegation:$false `
                -Credential $DomainCredential `
                -DatabasePath "C:\Windows\NTDS" `
                -DomainName $Domain `
                -InstallDns:$true `
                -LogPath "C:\Windows\NTDS" `
                -NoGlobalCatalog:$false `
                -NoRebootOnCompletion:$false `
                -SysvolPath "C:\Windows\SYSVOL" `
                -SafeModeAdministratorPassword $secureDsrmPassword `
                -Force:$true
        } -ArgumentList $DomainName, $DomainAdministratorCredential, $SafeModePassword -ErrorAction Stop
    }
    catch {
        Write-ExpectedRestartWarning -Activity "Replica promotion command for $VMName" -ErrorRecord $_
    }

    Wait-ForPowerShellDirect -VMName $VMName -Credential $DomainAdministratorCredential -Retries 90
    Wait-ForDomainReady -VMName $VMName -Credential $DomainAdministratorCredential -DomainName $DomainName
}

function Ensure-ChildDomain {
    param(
        [Parameter(Mandatory = $true)][string]$VMName,
        [Parameter(Mandatory = $true)][string]$ChildDomainName,
        [Parameter(Mandatory = $true)][string]$ChildNetBIOSName,
        [Parameter(Mandatory = $true)][string]$ParentDomainName,
        [Parameter(Mandatory = $true)][string]$NewDomainName,
        [Parameter(Mandatory = $true)][PSCredential]$LocalCredential,
        [Parameter(Mandatory = $true)][PSCredential]$ParentDomainAdministratorCredential,
        [Parameter(Mandatory = $true)][PSCredential]$ChildDomainAdministratorCredential,
        [Parameter(Mandatory = $true)][string]$SafeModePassword
    )

    if (Test-IsDomainControllerForDomain -VMName $VMName -Credential $ChildDomainAdministratorCredential -DomainName $ChildDomainName) {
        Write-Info "$VMName is already a domain controller for $ChildDomainName. Skipping child domain creation."
        return
    }

    Write-Info "Creating child domain '$ChildDomainName' on $VMName."
    Wait-ForDomainDnsReady -VMName $VMName -Credential $LocalCredential -DomainName $ParentDomainName -Retries 24 -DelaySeconds 5

    try {
        Invoke-Command -VMName $VMName -Credential $LocalCredential -ScriptBlock {
            param(
                [string]$ParentDomain,
                [string]$NewChildName,
                [string]$ChildNetBIOS,
                [PSCredential]$ParentCredential,
                [string]$DsrmPassword
            )

            Import-Module ADDSDeployment -ErrorAction Stop

            $secureDsrmPassword = ConvertTo-SecureString $DsrmPassword -AsPlainText -Force

            Install-ADDSDomain `
                -CreateDnsDelegation:$true `
                -Credential $ParentCredential `
                -DatabasePath "C:\Windows\NTDS" `
                -DomainMode "WinThreshold" `
                -DomainType ChildDomain `
                -InstallDns:$true `
                -LogPath "C:\Windows\NTDS" `
                -NewDomainName $NewChildName `
                -NewDomainNetbiosName $ChildNetBIOS `
                -NoRebootOnCompletion:$false `
                -ParentDomainName $ParentDomain `
                -SysvolPath "C:\Windows\SYSVOL" `
                -SafeModeAdministratorPassword $secureDsrmPassword `
                -Force:$true
        } -ArgumentList $ParentDomainName, $NewDomainName, $ChildNetBIOSName, $ParentDomainAdministratorCredential, $SafeModePassword -ErrorAction Stop
    }
    catch {
        Write-ExpectedRestartWarning -Activity "Child domain creation command for $VMName" -ErrorRecord $_
    }

    Wait-ForPowerShellDirect -VMName $VMName -Credential $ChildDomainAdministratorCredential -Retries 90
    Wait-ForDomainReady -VMName $VMName -Credential $ChildDomainAdministratorCredential -DomainName $ChildDomainName
}

function Ensure-DomainAdminUser {
    param(
        [Parameter(Mandatory = $true)][string]$VMName,
        [Parameter(Mandatory = $true)][PSCredential]$DomainAdministratorCredential,
        [Parameter(Mandatory = $true)][string]$DomainName,
        [Parameter(Mandatory = $true)][string]$UserName,
        [Parameter(Mandatory = $true)][string]$Password
    )

    Write-Info "Ensuring convenience domain admin '$UserName' exists in $DomainName."

    Invoke-Command -VMName $VMName -Credential $DomainAdministratorCredential -ScriptBlock {
        param(
            [string]$Domain,
            [string]$User,
            [string]$PlainTextPassword
        )

        Import-Module ActiveDirectory -ErrorAction Stop

        $existingUser = Get-ADUser -Filter "SamAccountName -eq '$User'" -ErrorAction Stop

        if (-not $existingUser) {
            $securePassword = ConvertTo-SecureString $PlainTextPassword -AsPlainText -Force

            New-ADUser `
                -Name $User `
                -SamAccountName $User `
                -UserPrincipalName "$User@$Domain" `
                -AccountPassword $securePassword `
                -Enabled $true `
                -PasswordNeverExpires $true `
                -ErrorAction Stop
        }

        $domainAdmins = Get-ADGroup -Identity "Domain Admins" -ErrorAction Stop
        $alreadyMember = Get-ADGroupMember -Identity $domainAdmins -Recursive | Where-Object { $_.SamAccountName -eq $User }

        if (-not $alreadyMember) {
            Add-ADGroupMember -Identity $domainAdmins -Members $User -ErrorAction Stop
        }

        Write-Output "Convenience domain admin is ready: $User@$Domain."
    } -ArgumentList $DomainName, $UserName, $Password -ErrorAction Stop
}

function Ensure-DomainJoin {
    param(
        [Parameter(Mandatory = $true)][string]$VMName,
        [Parameter(Mandatory = $true)][string]$DomainName,
        [Parameter(Mandatory = $true)][PSCredential]$LocalCredential,
        [Parameter(Mandatory = $true)][PSCredential]$DomainAdministratorCredential
    )

    $isJoined = $false

    try {
        $isJoined = Invoke-Command -VMName $VMName -Credential $LocalCredential -ScriptBlock {
            param([string]$ExpectedDomain)

            $computerSystem = Get-CimInstance Win32_ComputerSystem
            return ($computerSystem.PartOfDomain -and ($computerSystem.Domain -ieq $ExpectedDomain))
        } -ArgumentList $DomainName -ErrorAction Stop
    }
    catch {
        try {
            $isJoined = Invoke-Command -VMName $VMName -Credential $DomainAdministratorCredential -ScriptBlock {
                param([string]$ExpectedDomain)

                $computerSystem = Get-CimInstance Win32_ComputerSystem
                return ($computerSystem.PartOfDomain -and ($computerSystem.Domain -ieq $ExpectedDomain))
            } -ArgumentList $DomainName -ErrorAction Stop
        }
        catch {
            $isJoined = $false
        }
    }

    if ($isJoined) {
        Write-Info "$VMName is already joined to $DomainName."
        return
    }

    Write-Info "Joining $VMName to domain $DomainName."

    try {
        Invoke-Command -VMName $VMName -Credential $LocalCredential -ScriptBlock {
            param([string]$Domain, [PSCredential]$DomainCredential)

            Add-Computer `
                -DomainName $Domain `
                -Credential $DomainCredential `
                -Force `
                -ErrorAction Stop

            Restart-Computer -Force
        } -ArgumentList $DomainName, $DomainAdministratorCredential -ErrorAction Stop
    }
    catch {
        Write-ExpectedRestartWarning -Activity "Domain join command for $VMName" -ErrorRecord $_
    }

    Wait-ForPowerShellDirect -VMName $VMName -Credential $LocalCredential -Retries 60

    $joinedAfterRestart = Invoke-WithRetry `
        -Activity "Validating domain join for $VMName" `
        -Retries 24 `
        -DelaySeconds 5 `
        -Script {
            Invoke-Command -VMName $VMName -Credential $LocalCredential -ScriptBlock {
                param([string]$ExpectedDomain)

                $computerSystem = Get-CimInstance Win32_ComputerSystem
                return ($computerSystem.PartOfDomain -and ($computerSystem.Domain -ieq $ExpectedDomain))
            } -ArgumentList $DomainName -ErrorAction Stop
        }

    Invoke-WithRetry `
        -Activity "Validating domain credential for $VMName" `
        -Retries 24 `
        -DelaySeconds 5 `
        -Script {
            Invoke-Command -VMName $VMName -Credential $DomainAdministratorCredential -ScriptBlock {
                param([string]$ExpectedDomain)

                $computerSystem = Get-CimInstance Win32_ComputerSystem

                if (-not ($computerSystem.PartOfDomain -and ($computerSystem.Domain -ieq $ExpectedDomain))) {
                    throw "$env:COMPUTERNAME is not joined to $ExpectedDomain."
                }
            } -ArgumentList $DomainName -ErrorAction Stop
        } | Out-Null

    if (-not $joinedAfterRestart) {
        throw "$VMName did not join domain $DomainName successfully."
    }

    Write-Info "$VMName successfully joined $DomainName."
}

function Set-GuestDnsBySwitch {
    param(
        [Parameter(Mandatory = $true)][string]$VMName,
        [Parameter(Mandatory = $true)][string]$SwitchName,
        [Parameter(Mandatory = $true)][string[]]$DnsServers,
        [Parameter(Mandatory = $true)][PSCredential]$Credential
    )

    $guestMac = Get-VMNicMacBySwitch -VMName $VMName -SwitchName $SwitchName

    Invoke-Command -VMName $VMName -Credential $Credential -ScriptBlock {
        param([string]$MacAddress, [string[]]$Servers)

        $adapter = Get-NetAdapter | Where-Object { $_.MacAddress -eq $MacAddress } | Select-Object -First 1

        if (-not $adapter) {
            throw "No guest adapter found with MAC address $MacAddress."
        }

        Set-DnsClientServerAddress `
            -InterfaceIndex $adapter.ifIndex `
            -ServerAddresses $Servers `
            -ErrorAction Stop

        Write-Output "DNS configured on $($adapter.Name): $($Servers -join ', ')."
    } -ArgumentList $guestMac, (, $DnsServers) -ErrorAction Stop

    Write-Info "DNS updated on ${VMName}: $($DnsServers -join ', ')"
}

############################################################
# LAB TASK ORCHESTRATION
############################################################

function New-LabTask {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][scriptblock]$ScriptBlock,
        [object[]]$ArgumentList = @()
    )

    return [pscustomobject]@{
        Name         = $Name
        ScriptBlock  = $ScriptBlock
        ArgumentList = $ArgumentList
    }
}

function New-LabJobInitializationScript {
    $functionNames = @(
        "Write-Stage",
        "Write-Info",
        "Write-Warn",
        "Write-Fail",
        "New-PlainTextCredential",
        "Get-LocalCredential",
        "Get-DomainAdministratorCredential",
        "Get-ConvenienceDomainCredential",
        "Invoke-WithRetry",
        "Wait-ForPowerShellDirect",
        "Test-CanUsePowerShellDirect",
        "Test-IsExpectedRestartDisconnect",
        "Write-ExpectedRestartWarning",
        "Convert-ToGuestMacAddress",
        "Get-VMNicMacBySwitch",
        "Test-LabTopology",
        "Initialize-VMNetworkBySwitch",
        "Assert-VMNetworkState",
        "Initialize-RouterNetwork",
        "Enable-BaseRemoteAccess",
        "Ensure-RouterRole",
        "Enable-VMRouting",
        "Enable-VMNat",
        "Ensure-WindowsFeatures",
        "Test-IsDomainControllerForDomain",
        "Wait-ForDomainReady",
        "Wait-ForDomainDnsReady",
        "Ensure-NewForest",
        "Ensure-ReplicaDomainController",
        "Ensure-ChildDomain",
        "Ensure-DomainAdminUser",
        "Ensure-DomainJoin",
        "Set-GuestDnsBySwitch"
    )

    $definitions = foreach ($functionName in $functionNames) {
        $command = Get-Command -Name $functionName -CommandType Function -ErrorAction Stop
        "function $functionName {`n$($command.ScriptBlock.ToString())`n}"
    }

    return [scriptblock]::Create(($definitions -join "`n`n"))
}

function Invoke-LabTaskGroup {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][object[]]$Tasks,
        [int]$TimeoutSeconds = 0
    )

    $taskList = @($Tasks)

    if ($taskList.Count -eq 0) {
        Write-Info "Task group '$Name' has no tasks. Skipping."
        return
    }

    Write-Stage "TASK GROUP - $Name"
    Write-Info "Starting $($taskList.Count) task(s): $((@($taskList | ForEach-Object { $_.Name })) -join ', ')"

    $groupTimer = [System.Diagnostics.Stopwatch]::StartNew()
    $initializationScriptText = (New-LabJobInitializationScript).ToString()
    $runningTasks = @()

    try {
        foreach ($task in $taskList) {
            Write-Info "Queueing task '$($task.Name)'."

            $runspace = [runspacefactory]::CreateRunspace()
            $runspace.Open()

            $powershell = [powershell]::Create()
            $powershell.Runspace = $runspace

            [void]$powershell.AddScript($initializationScriptText)
            [void]$powershell.Invoke()

            if ($powershell.HadErrors) {
                $errors = @($powershell.Streams.Error | ForEach-Object { $_.Exception.Message }) -join "`n"
                throw "Failed to initialize task '$($task.Name)' runspace. $errors"
            }

            $powershell.Commands.Clear()
            $powershell.Streams.ClearStreams()

            [void]$powershell.AddScript({
                param(
                    [string]$TaskName,
                    [string]$TaskScriptText,
                    [object[]]$TaskArguments
                )

                Set-StrictMode -Version Latest
                $ErrorActionPreference = "Stop"

                Write-Info "Task '$TaskName' started."
                $taskTimer = [System.Diagnostics.Stopwatch]::StartNew()
                $taskScript = [scriptblock]::Create($TaskScriptText)
                try {
                    & $taskScript @TaskArguments
                }
                finally {
                    $taskTimer.Stop()
                    Write-Info "Task '$TaskName' elapsed: $([int]$taskTimer.Elapsed.TotalSeconds)s."
                }

                Write-Info "Task '$TaskName' completed."
            })
            [void]$powershell.AddArgument($task.Name)
            [void]$powershell.AddArgument($task.ScriptBlock.ToString())
            [void]$powershell.AddArgument(@($task.ArgumentList))

            $runningTasks += [pscustomobject]@{
                Name        = $task.Name
                PowerShell  = $powershell
                Runspace    = $runspace
                AsyncResult = $powershell.BeginInvoke()
                TimedOut    = $false
            }
        }

        if ($TimeoutSeconds -gt 0) {
            $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
        }

        do {
            $incompleteTasks = @($runningTasks | Where-Object { -not $_.AsyncResult.IsCompleted })

            if ($incompleteTasks.Count -eq 0) {
                break
            }

            if (($TimeoutSeconds -gt 0) -and ([DateTime]::UtcNow -ge $deadline)) {
                foreach ($runningTask in $incompleteTasks) {
                    $runningTask.TimedOut = $true
                    $runningTask.PowerShell.Stop()
                }

                break
            }

            Start-Sleep -Seconds 1
        }
        while ($true)

        $failures = @()

        foreach ($runningTask in $runningTasks) {
            if ($runningTask.TimedOut) {
                $failures += [pscustomobject]@{
                    Name  = $runningTask.Name
                    Error = "Task timed out after $TimeoutSeconds seconds."
                }

                continue
            }

            $endInvokeFailed = $false

            try {
                $output = $runningTask.PowerShell.EndInvoke($runningTask.AsyncResult)

                foreach ($item in $output) {
                    Write-Output $item
                }
            }
            catch {
                $failures += [pscustomobject]@{
                    Name  = $runningTask.Name
                    Error = $_.Exception.Message
                }

                $endInvokeFailed = $true
            }

            if ((-not $endInvokeFailed) -and $runningTask.PowerShell.HadErrors) {
                $errorText = @($runningTask.PowerShell.Streams.Error | ForEach-Object { $_.Exception.Message }) -join "`n"

                if (-not [string]::IsNullOrWhiteSpace($errorText)) {
                    $failures += [pscustomobject]@{
                        Name  = $runningTask.Name
                        Error = $errorText
                    }
                }
            }
        }

        if ($failures.Count -gt 0) {
            $groupTimer.Stop()
            $failureText = @($failures | ForEach-Object { "$($_.Name): $($_.Error)" }) -join "`n"
            throw "Task group '$Name' failed after $([int]$groupTimer.Elapsed.TotalSeconds)s:`n$failureText"
        }

        $groupTimer.Stop()
        Write-Info "Task group '$Name' completed successfully in $([int]$groupTimer.Elapsed.TotalSeconds)s."
    }
    finally {
        foreach ($runningTask in $runningTasks) {
            if ($runningTask.PowerShell) {
                $runningTask.PowerShell.Dispose()
            }

            if ($runningTask.Runspace) {
                $runningTask.Runspace.Close()
                $runningTask.Runspace.Dispose()
            }
        }
    }
}

function Show-LabExecutionPlan {
    param([Parameter(Mandatory = $true)]$Config)

    $contosoDomain = $Config.Domains.Contoso.DomainName

    Write-Stage "LAB EXECUTION PLAN"
    Write-Info "Stage 0: Preflight validation runs sequentially for deterministic topology and reachability failures."
    Write-Info "Stage 1: NetworkInitialization runs guest VM network tasks plus router network initialization concurrently."
    Write-Info "Stage 2: Router services run sequentially on $($Config.Router.Name), followed by base remote access configuration."
    Write-Info "Stage 3: DomainFeatureInstall installs AD DS on DC candidates concurrently."
    Write-Info "Stage 4: IndependentForestCreation creates Contoso and Fabrikam forests concurrently."
    Write-Info "Stage 5: PostForestAdminUsers creates Contoso and Fabrikam convenience admins concurrently."
    Write-Info "Stage 6: ContosoReplicaPromotion promotes Contoso replicas concurrently after $contosoDomain exists, then updates Contoso DC DNS."
    Write-Info "Stage 7: Child domain creation waits for Contoso and remains sequential for its DC."
    Write-Info "Stage 8: MemberDomainJoin joins Contoso member servers concurrently after Contoso DNS is stabilized."
    Write-Info "Stage 9: PkiFeatureInstall installs PKI-related roles concurrently after member joins."
}

############################################################
# MAIN ORCHESTRATION
############################################################

function Build-Lab {
    param(
        [Parameter(Mandatory = $true)]$Config,
        [switch]$PlanOnly
    )

    if ($PlanOnly) {
        Show-LabExecutionPlan -Config $Config
        return
    }

    $localCredential = Get-LocalCredential -Config $Config

    $contosoDomain = $Config.Domains.Contoso.DomainName
    $fabrikamDomain = $Config.Domains.Fabrikam.DomainName
    $childDomain = $Config.Domains.Child.DomainName

    $contosoAdminCredential = Get-DomainAdministratorCredential -Config $Config -DomainName $contosoDomain
    $fabrikamAdminCredential = Get-DomainAdministratorCredential -Config $Config -DomainName $fabrikamDomain
    $childAdminCredential = Get-DomainAdministratorCredential -Config $Config -DomainName $childDomain

    $allVmNames = @($Config.VMs.Keys) + @($Config.Router.Name)
    $preflightCredentialMap = @{}

    foreach ($vmName in $allVmNames) {
        $preflightCredentialMap[$vmName] = @($localCredential)
    }

    foreach ($vmName in $Config.VMs.Keys) {
        $vm = $Config.VMs[$vmName]

        if ($vm.ContainsKey("DomainJoin") -and $vm.DomainJoin -eq $contosoDomain) {
            $preflightCredentialMap[$vmName] = @($localCredential, $contosoAdminCredential)
        }
    }

    $preflightCredentialMap[$Config.Domains.Contoso.FirstDC] = @($localCredential, $contosoAdminCredential)

    foreach ($replicaVmName in $Config.Domains.Contoso.ReplicaDCs) {
        $preflightCredentialMap[$replicaVmName] = @($localCredential, $contosoAdminCredential)
    }

    $preflightCredentialMap[$Config.Domains.Fabrikam.FirstDC] = @($localCredential, $fabrikamAdminCredential)
    $preflightCredentialMap[$Config.Domains.Child.FirstDC] = @($localCredential, $childAdminCredential, $contosoAdminCredential)

    Test-LabTopology -Config $Config -CredentialMap $preflightCredentialMap

    ########################################################
    # STAGE 1 - GUEST NETWORK INITIALIZATION
    ########################################################

    $networkTasks = @()

    foreach ($vmName in $Config.VMs.Keys) {
        $vm = $Config.VMs[$vmName]

        $networkTasks += New-LabTask `
            -Name "Network:$vmName" `
            -ScriptBlock {
                param($TaskVMName, $TaskVM, [PSCredential]$TaskCredential)

                Initialize-VMNetworkBySwitch `
                    -VMName $TaskVMName `
                    -SwitchName $TaskVM.SwitchName `
                    -IPAddress $TaskVM.IP `
                    -PrefixLength ([int]$TaskVM.PrefixLength) `
                    -DefaultGateway $TaskVM.Gateway `
                    -DnsServers $TaskVM.DNS `
                    -ComputerName $TaskVMName `
                    -Credential $TaskCredential
            } `
            -ArgumentList @($vmName, $vm, $localCredential)
    }

    $networkTasks += New-LabTask `
        -Name "Network:$($Config.Router.Name)" `
        -ScriptBlock {
            param($TaskConfig, [PSCredential]$TaskCredential)

            Initialize-RouterNetwork -Config $TaskConfig -Credential $TaskCredential
        } `
        -ArgumentList @($Config, $localCredential)

    Invoke-LabTaskGroup -Name "NetworkInitialization" -Tasks $networkTasks

    ########################################################
    # STAGE 2 - BASE REMOTE ACCESS AND ROUTER SERVICES
    ########################################################

    Write-Stage "STAGE 2 - ROUTER SERVICES"

    Ensure-RouterRole `
        -VMName $Config.Router.Name `
        -Credential $localCredential

    Enable-VMRouting `
        -VMName $Config.Router.Name `
        -Credential $localCredential `
        -ExternalSwitchName $Config.Router.ExternalSwitchName

    Enable-VMNat `
        -Config $Config `
        -Credential $localCredential

    Write-Stage "STAGE 2 - BASE REMOTE ACCESS"

    Enable-BaseRemoteAccess `
        -VMNames $allVmNames `
        -Credential $localCredential `
        -DisableFirewall ([bool]$Config.Behavior.DisableFirewall) `
        -DisableRdpNla ([bool]$Config.Behavior.DisableRdpNla)

    ########################################################
    # STAGE 3 - AD DS ROLE INSTALLATION
    ########################################################

    $domainControllerVMs = @(
        $Config.Domains.Contoso.FirstDC
    ) + $Config.Domains.Contoso.ReplicaDCs + @(
        $Config.Domains.Fabrikam.FirstDC,
        $Config.Domains.Child.FirstDC
    )

    $domainFeatureTasks = @()

    foreach ($dcVmName in $domainControllerVMs) {
        $domainFeatureTasks += New-LabTask `
            -Name "ADDSFeature:$dcVmName" `
            -ScriptBlock {
                param([string]$TaskVMName, [PSCredential]$TaskCredential)

                Ensure-WindowsFeatures `
                    -VMName $TaskVMName `
                    -Credential $TaskCredential `
                    -FeatureNames @("AD-Domain-Services") `
                    -IncludeManagementTools
            } `
            -ArgumentList @($dcVmName, $localCredential)
    }

    Invoke-LabTaskGroup -Name "DomainFeatureInstall" -Tasks $domainFeatureTasks

    ########################################################
    # STAGE 4 - INDEPENDENT ROOT FORESTS
    ########################################################

    $forestTasks = @()

    $forestTasks += New-LabTask `
        -Name "Forest:Contoso" `
        -ScriptBlock {
            param($TaskDomainConfig, [PSCredential]$TaskLocalCredential, [PSCredential]$TaskDomainCredential, [string]$TaskSafeModePassword)

            Ensure-NewForest `
                -VMName $TaskDomainConfig.FirstDC `
                -DomainName $TaskDomainConfig.DomainName `
                -NetBIOSName $TaskDomainConfig.NetBIOS `
                -LocalCredential $TaskLocalCredential `
                -DomainAdministratorCredential $TaskDomainCredential `
                -SafeModePassword $TaskSafeModePassword
        } `
        -ArgumentList @($Config.Domains.Contoso, $localCredential, $contosoAdminCredential, $Config.Credentials.SafeModePassword)

    $forestTasks += New-LabTask `
        -Name "Forest:Fabrikam" `
        -ScriptBlock {
            param($TaskDomainConfig, [PSCredential]$TaskLocalCredential, [PSCredential]$TaskDomainCredential, [string]$TaskSafeModePassword)

            Ensure-NewForest `
                -VMName $TaskDomainConfig.FirstDC `
                -DomainName $TaskDomainConfig.DomainName `
                -NetBIOSName $TaskDomainConfig.NetBIOS `
                -LocalCredential $TaskLocalCredential `
                -DomainAdministratorCredential $TaskDomainCredential `
                -SafeModePassword $TaskSafeModePassword
        } `
        -ArgumentList @($Config.Domains.Fabrikam, $localCredential, $fabrikamAdminCredential, $Config.Credentials.SafeModePassword)

    Invoke-LabTaskGroup -Name "IndependentForestCreation" -Tasks $forestTasks

    ########################################################
    # STAGE 5 - POST-FOREST ADMIN USERS
    ########################################################

    $postForestAdminTasks = @()

    $postForestAdminTasks += New-LabTask `
        -Name "AdminUser:Contoso" `
        -ScriptBlock {
            param([string]$TaskVMName, [PSCredential]$TaskDomainCredential, [string]$TaskDomainName, [string]$TaskUserName, [string]$TaskPassword)

            Ensure-DomainAdminUser `
                -VMName $TaskVMName `
                -DomainAdministratorCredential $TaskDomainCredential `
                -DomainName $TaskDomainName `
                -UserName $TaskUserName `
                -Password $TaskPassword
        } `
        -ArgumentList @($Config.Domains.Contoso.FirstDC, $contosoAdminCredential, $contosoDomain, $Config.Credentials.ConvenienceAdminUser, $Config.Credentials.ConvenienceAdminPassword)

    $postForestAdminTasks += New-LabTask `
        -Name "AdminUser:Fabrikam" `
        -ScriptBlock {
            param([string]$TaskVMName, [PSCredential]$TaskDomainCredential, [string]$TaskDomainName, [string]$TaskUserName, [string]$TaskPassword)

            Ensure-DomainAdminUser `
                -VMName $TaskVMName `
                -DomainAdministratorCredential $TaskDomainCredential `
                -DomainName $TaskDomainName `
                -UserName $TaskUserName `
                -Password $TaskPassword
        } `
        -ArgumentList @($Config.Domains.Fabrikam.FirstDC, $fabrikamAdminCredential, $fabrikamDomain, $Config.Credentials.ConvenienceAdminUser, $Config.Credentials.ConvenienceAdminPassword)

    Invoke-LabTaskGroup -Name "PostForestAdminUsers" -Tasks $postForestAdminTasks

    ########################################################
    # STAGE 6 - CONTOSO REPLICA DOMAIN CONTROLLERS
    ########################################################

    $replicaTasks = @()

    foreach ($replicaVmName in $Config.Domains.Contoso.ReplicaDCs) {
        $replicaTasks += New-LabTask `
            -Name "ReplicaDC:$replicaVmName" `
            -ScriptBlock {
                param([string]$TaskVMName, [string]$TaskDomainName, [PSCredential]$TaskLocalCredential, [PSCredential]$TaskDomainCredential, [string]$TaskSafeModePassword)

                Ensure-ReplicaDomainController `
                    -VMName $TaskVMName `
                    -DomainName $TaskDomainName `
                    -LocalCredential $TaskLocalCredential `
                    -DomainAdministratorCredential $TaskDomainCredential `
                    -SafeModePassword $TaskSafeModePassword
            } `
            -ArgumentList @($replicaVmName, $contosoDomain, $localCredential, $contosoAdminCredential, $Config.Credentials.SafeModePassword)
    }

    Invoke-LabTaskGroup -Name "ContosoReplicaPromotion" -Tasks $replicaTasks

    $contosoDnsTasks = @()

    $contosoDnsTasks += New-LabTask `
        -Name "Dns:ContosoDC1" `
        -ScriptBlock {
            param([string]$TaskVMName, [string]$TaskSwitchName, [string[]]$TaskDnsServers, [PSCredential]$TaskCredential)

            Set-GuestDnsBySwitch `
                -VMName $TaskVMName `
                -SwitchName $TaskSwitchName `
                -DnsServers $TaskDnsServers `
                -Credential $TaskCredential
        } `
        -ArgumentList @("ContosoDC1", $Config.VMs.ContosoDC1.SwitchName, @("127.0.0.1", "10.0.0.3"), $contosoAdminCredential)

    $contosoDnsTasks += New-LabTask `
        -Name "Dns:ContosoDC2" `
        -ScriptBlock {
            param([string]$TaskVMName, [string]$TaskSwitchName, [string[]]$TaskDnsServers, [PSCredential]$TaskCredential)

            Set-GuestDnsBySwitch `
                -VMName $TaskVMName `
                -SwitchName $TaskSwitchName `
                -DnsServers $TaskDnsServers `
                -Credential $TaskCredential
        } `
        -ArgumentList @("ContosoDC2", $Config.VMs.ContosoDC2.SwitchName, @("127.0.0.1", "10.0.0.2"), $contosoAdminCredential)

    Invoke-LabTaskGroup -Name "ContosoDnsStabilization" -Tasks $contosoDnsTasks

    ########################################################
    # STAGE 6B - FABRIKAM DNS
    ########################################################

    Set-GuestDnsBySwitch `
        -VMName $Config.Domains.Fabrikam.FirstDC `
        -SwitchName $Config.VMs.FabrikamDC1.SwitchName `
        -DnsServers @("127.0.0.1") `
        -Credential $fabrikamAdminCredential

    ########################################################
    # STAGE 7 - CHILD DOMAIN
    ########################################################

    Write-Stage "STAGE 7 - CHILD DOMAIN"

    Set-GuestDnsBySwitch `
        -VMName $Config.Domains.Child.FirstDC `
        -SwitchName $Config.VMs.ChildDC1.SwitchName `
        -DnsServers @("10.0.0.2", "10.0.0.3") `
        -Credential $localCredential

    Ensure-ChildDomain `
        -VMName $Config.Domains.Child.FirstDC `
        -ChildDomainName $Config.Domains.Child.DomainName `
        -ChildNetBIOSName $Config.Domains.Child.NetBIOS `
        -ParentDomainName $Config.Domains.Child.ParentDomainName `
        -NewDomainName $Config.Domains.Child.NewDomainName `
        -LocalCredential $localCredential `
        -ParentDomainAdministratorCredential $contosoAdminCredential `
        -ChildDomainAdministratorCredential $childAdminCredential `
        -SafeModePassword $Config.Credentials.SafeModePassword

    Set-GuestDnsBySwitch `
        -VMName $Config.Domains.Child.FirstDC `
        -SwitchName $Config.VMs.ChildDC1.SwitchName `
        -DnsServers @("127.0.0.1", "10.0.0.2", "10.0.0.3") `
        -Credential $childAdminCredential

    Ensure-DomainAdminUser `
        -VMName $Config.Domains.Child.FirstDC `
        -DomainAdministratorCredential $childAdminCredential `
        -DomainName $childDomain `
        -UserName $Config.Credentials.ConvenienceAdminUser `
        -Password $Config.Credentials.ConvenienceAdminPassword

    ########################################################
    # STAGE 8 - CONTOSO MEMBER SERVER DOMAIN JOIN
    ########################################################

    $memberJoinTasks = @()

    foreach ($vmName in $Config.VMs.Keys) {
        $vm = $Config.VMs[$vmName]

        if ($vm.ContainsKey("DomainJoin") -and $vm.DomainJoin -eq $contosoDomain) {
            $memberJoinTasks += New-LabTask `
                -Name "DomainJoin:$vmName" `
                -ScriptBlock {
                    param([string]$TaskVMName, [string]$TaskDomainName, [PSCredential]$TaskLocalCredential, [PSCredential]$TaskDomainCredential)

                    Ensure-DomainJoin `
                        -VMName $TaskVMName `
                        -DomainName $TaskDomainName `
                        -LocalCredential $TaskLocalCredential `
                        -DomainAdministratorCredential $TaskDomainCredential
                } `
                -ArgumentList @($vmName, $contosoDomain, $localCredential, $contosoAdminCredential)
        }
    }

    Invoke-LabTaskGroup -Name "MemberDomainJoin" -Tasks $memberJoinTasks

    ########################################################
    # STAGE 9 - PKI ROLE INSTALLATION ONLY
    ########################################################

    $pkiTasks = @()

    $pkiTasks += New-LabTask `
        -Name "PkiFeature:RootCA" `
        -ScriptBlock {
            param([string]$TaskVMName, [PSCredential]$TaskCredential)

            Ensure-WindowsFeatures `
                -VMName $TaskVMName `
                -Credential $TaskCredential `
                -FeatureNames @("ADCS-Cert-Authority") `
                -IncludeManagementTools
        } `
        -ArgumentList @("RootCA", $localCredential)

    $pkiTasks += New-LabTask `
        -Name "PkiFeature:ContSubCA" `
        -ScriptBlock {
            param([string]$TaskVMName, [PSCredential]$TaskCredential)

            Ensure-WindowsFeatures `
                -VMName $TaskVMName `
                -Credential $TaskCredential `
                -FeatureNames @("ADCS-Cert-Authority") `
                -IncludeManagementTools
        } `
        -ArgumentList @("ContSubCA", $contosoAdminCredential)

    $pkiTasks += New-LabTask `
        -Name "PkiFeature:PKIOperations" `
        -ScriptBlock {
            param([string]$TaskVMName, [PSCredential]$TaskCredential)

            Ensure-WindowsFeatures `
                -VMName $TaskVMName `
                -Credential $TaskCredential `
                -FeatureNames @(
                    "Web-Server",
                    "ADCS-Enroll-Web-Pol",
                    "ADCS-Enroll-Web-Svc",
                    "ADCS-Device-Enrollment"
                ) `
                -IncludeManagementTools
        } `
        -ArgumentList @("PKIOperations", $contosoAdminCredential)

    Invoke-LabTaskGroup -Name "PkiFeatureInstall" -Tasks $pkiTasks

    ########################################################
    # COMPLETE
    ########################################################

    Write-Stage "LAB BUILD COMPLETE"
    Write-Info "Contoso forest: $contosoDomain"
    Write-Info "Fabrikam forest: $fabrikamDomain"
    Write-Info "Child domain: $childDomain"
    Write-Info "Convenience admin user created where applicable: $($Config.Credentials.ConvenienceAdminUser)"
    Write-Info "PKI-related roles installed only. AD CS configuration was intentionally not performed."
}

############################################################
# EXECUTION
############################################################

Build-Lab -Config $LabConfig -PlanOnly:$PlanOnly
