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

    for ($attempt = 1; $attempt -le $Retries; $attempt++) {
        try {
            return & $Script
        }
        catch {
            $lastError = $_
            Write-Warn "$Activity failed on attempt $attempt of $Retries. $($_.Exception.Message)"
            Start-Sleep -Seconds $DelaySeconds
        }
    }

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
    param([Parameter(Mandatory = $true)]$Config)

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
        Get-VM -Name $vmName -ErrorAction Stop | Out-Null
        Write-Info "Validated VM exists: $vmName"
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
        $mappedNics += @{
            SwitchName   = $nic.SwitchName
            MacAddress   = Get-VMNicMacBySwitch -VMName $routerName -SwitchName $nic.SwitchName
            IP           = $nic.IP
            PrefixLength = if ($nic.PrefixLength) { [int]$nic.PrefixLength } else { 24 }
            DNS          = $nic.DNS
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

            if ($entry.IP) {
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

    $internalMacsJson = $internalMacs | ConvertTo-Json -Depth 3

    Write-Info "Configuring RRAS NAT on $routerName."

    Invoke-Command -VMName $routerName -Credential $Credential -ScriptBlock {
        param(
            [string]$ExternalMacAddress,
            [string]$InternalMacsJson
        )

        $internalMacAddresses = @($InternalMacsJson | ConvertFrom-Json)

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

        netsh routing ip nat install | Out-Null

        netsh routing ip nat delete interface "$($externalAdapter.Name)" 2>$null | Out-Null
        netsh routing ip nat add interface "$($externalAdapter.Name)" mode=full | Out-Null

        foreach ($adapter in $internalAdapters) {
            netsh routing ip nat delete interface "$($adapter.Name)" 2>$null | Out-Null
            netsh routing ip nat add interface "$($adapter.Name)" mode=private | Out-Null
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
                Resolve-DnsName "_ldap._tcp.dc._msdcs.$ExpectedDomainName" -ErrorAction Stop | Out-Null

                Import-Module ActiveDirectory -ErrorAction Stop
                $domain = Get-ADDomain -ErrorAction Stop

                if ($domain.DNSRoot -ine $ExpectedDomainName) {
                    throw "Expected domain '$ExpectedDomainName', but local domain is '$($domain.DNSRoot)'."
                }
            } -ArgumentList $DomainName -ErrorAction Stop
        }

    Write-Info "Domain is ready: $DomainName."
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

    if (Test-IsDomainControllerForDomain -VMName $VMName -Credential $DomainAdministratorCredential -DomainName $DomainName) {
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
        Write-Warn "Forest creation command for $VMName ended with: $($_.Exception.Message)"
        Write-Warn "This can be expected when the VM restarts during promotion. Domain readiness will be validated next."
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
        Write-Warn "Replica promotion command for $VMName ended with: $($_.Exception.Message)"
        Write-Warn "This can be expected when the VM restarts during promotion. Domain readiness will be validated next."
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
        Write-Warn "Child domain creation command for $VMName ended with: $($_.Exception.Message)"
        Write-Warn "This can be expected when the VM restarts during promotion. Domain readiness will be validated next."
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
        $isJoined = Invoke-Command -VMName $VMName -Credential $DomainAdministratorCredential -ScriptBlock {
            param([string]$ExpectedDomain)

            $computerSystem = Get-CimInstance Win32_ComputerSystem
            return ($computerSystem.PartOfDomain -and ($computerSystem.Domain -ieq $ExpectedDomain))
        } -ArgumentList $DomainName -ErrorAction Stop
    }
    catch {
        $isJoined = $false
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
        Write-Warn "Domain join command for $VMName ended with: $($_.Exception.Message)"
        Write-Warn "This can be expected when the VM restarts during domain join. Domain membership will be validated next."
    }

    Wait-ForPowerShellDirect -VMName $VMName -Credential $DomainAdministratorCredential -Retries 60

    $joinedAfterRestart = Invoke-Command -VMName $VMName -Credential $DomainAdministratorCredential -ScriptBlock {
        param([string]$ExpectedDomain)

        $computerSystem = Get-CimInstance Win32_ComputerSystem
        return ($computerSystem.PartOfDomain -and ($computerSystem.Domain -ieq $ExpectedDomain))
    } -ArgumentList $DomainName -ErrorAction Stop

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
# MAIN ORCHESTRATION
############################################################

function Build-Lab {
    param([Parameter(Mandatory = $true)]$Config)

    $localCredential = Get-LocalCredential -Config $Config

    $contosoDomain = $Config.Domains.Contoso.DomainName
    $fabrikamDomain = $Config.Domains.Fabrikam.DomainName
    $childDomain = $Config.Domains.Child.DomainName

    $contosoAdminCredential = Get-DomainAdministratorCredential -Config $Config -DomainName $contosoDomain
    $fabrikamAdminCredential = Get-DomainAdministratorCredential -Config $Config -DomainName $fabrikamDomain
    $childAdminCredential = Get-DomainAdministratorCredential -Config $Config -DomainName $childDomain

    $allVmNames = @($Config.VMs.Keys) + @($Config.Router.Name)

    Test-LabTopology -Config $Config

    ########################################################
    # PHASE 1 - GUEST NETWORK INITIALIZATION
    ########################################################

    Write-Stage "PHASE 1 - GUEST NETWORK INITIALIZATION"

    foreach ($vmName in $Config.VMs.Keys) {
        $vm = $Config.VMs[$vmName]

        Initialize-VMNetworkBySwitch `
            -VMName $vmName `
            -SwitchName $vm.SwitchName `
            -IPAddress $vm.IP `
            -PrefixLength ([int]$vm.PrefixLength) `
            -DefaultGateway $vm.Gateway `
            -DnsServers $vm.DNS `
            -ComputerName $vmName `
            -Credential $localCredential
    }

    Initialize-RouterNetwork -Config $Config -Credential $localCredential

    ########################################################
    # PHASE 2 - BASE REMOTE ACCESS AND ROUTER SERVICES
    ########################################################

    Write-Stage "PHASE 2 - BASE REMOTE ACCESS AND ROUTER SERVICES"

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

    Enable-BaseRemoteAccess `
        -VMNames $allVmNames `
        -Credential $localCredential `
        -DisableFirewall ([bool]$Config.Behavior.DisableFirewall) `
        -DisableRdpNla ([bool]$Config.Behavior.DisableRdpNla)

    ########################################################
    # PHASE 3 - AD DS ROLE INSTALLATION
    ########################################################

    Write-Stage "PHASE 3 - AD DS ROLE INSTALLATION"

    $domainControllerVMs = @(
        $Config.Domains.Contoso.FirstDC
    ) + $Config.Domains.Contoso.ReplicaDCs + @(
        $Config.Domains.Fabrikam.FirstDC,
        $Config.Domains.Child.FirstDC
    )

    foreach ($dcVmName in $domainControllerVMs) {
        Ensure-WindowsFeatures `
            -VMName $dcVmName `
            -Credential $localCredential `
            -FeatureNames @("AD-Domain-Services") `
            -IncludeManagementTools
    }

    ########################################################
    # PHASE 4 - CONTOSO FOREST
    ########################################################

    Write-Stage "PHASE 4 - CONTOSO FOREST"

    Ensure-NewForest `
        -VMName $Config.Domains.Contoso.FirstDC `
        -DomainName $Config.Domains.Contoso.DomainName `
        -NetBIOSName $Config.Domains.Contoso.NetBIOS `
        -LocalCredential $localCredential `
        -DomainAdministratorCredential $contosoAdminCredential `
        -SafeModePassword $Config.Credentials.SafeModePassword

    Ensure-DomainAdminUser `
        -VMName $Config.Domains.Contoso.FirstDC `
        -DomainAdministratorCredential $contosoAdminCredential `
        -DomainName $contosoDomain `
        -UserName $Config.Credentials.ConvenienceAdminUser `
        -Password $Config.Credentials.ConvenienceAdminPassword

    ########################################################
    # PHASE 5 - CONTOSO REPLICA DOMAIN CONTROLLERS
    ########################################################

    Write-Stage "PHASE 5 - CONTOSO REPLICA DOMAIN CONTROLLERS"

    foreach ($replicaVmName in $Config.Domains.Contoso.ReplicaDCs) {
        Ensure-ReplicaDomainController `
            -VMName $replicaVmName `
            -DomainName $contosoDomain `
            -LocalCredential $localCredential `
            -DomainAdministratorCredential $contosoAdminCredential `
            -SafeModePassword $Config.Credentials.SafeModePassword
    }

    Set-GuestDnsBySwitch `
        -VMName "ContosoDC1" `
        -SwitchName $Config.VMs.ContosoDC1.SwitchName `
        -DnsServers @("127.0.0.1", "10.0.0.3") `
        -Credential $contosoAdminCredential

    Set-GuestDnsBySwitch `
        -VMName "ContosoDC2" `
        -SwitchName $Config.VMs.ContosoDC2.SwitchName `
        -DnsServers @("127.0.0.1", "10.0.0.2") `
        -Credential $contosoAdminCredential

    ########################################################
    # PHASE 6 - FABRIKAM FOREST
    ########################################################

    Write-Stage "PHASE 6 - FABRIKAM FOREST"

    Ensure-NewForest `
        -VMName $Config.Domains.Fabrikam.FirstDC `
        -DomainName $Config.Domains.Fabrikam.DomainName `
        -NetBIOSName $Config.Domains.Fabrikam.NetBIOS `
        -LocalCredential $localCredential `
        -DomainAdministratorCredential $fabrikamAdminCredential `
        -SafeModePassword $Config.Credentials.SafeModePassword

    Set-GuestDnsBySwitch `
        -VMName $Config.Domains.Fabrikam.FirstDC `
        -SwitchName $Config.VMs.FabrikamDC1.SwitchName `
        -DnsServers @("127.0.0.1") `
        -Credential $fabrikamAdminCredential

    Ensure-DomainAdminUser `
        -VMName $Config.Domains.Fabrikam.FirstDC `
        -DomainAdministratorCredential $fabrikamAdminCredential `
        -DomainName $fabrikamDomain `
        -UserName $Config.Credentials.ConvenienceAdminUser `
        -Password $Config.Credentials.ConvenienceAdminPassword

    ########################################################
    # PHASE 7 - CHILD DOMAIN
    ########################################################

    Write-Stage "PHASE 7 - CHILD DOMAIN"

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
    # PHASE 8 - CONTOSO MEMBER SERVER DOMAIN JOIN
    ########################################################

    Write-Stage "PHASE 8 - CONTOSO MEMBER SERVER DOMAIN JOIN"

    foreach ($vmName in $Config.VMs.Keys) {
        $vm = $Config.VMs[$vmName]

        if ($vm.ContainsKey("DomainJoin") -and $vm.DomainJoin -eq $contosoDomain) {
            Ensure-DomainJoin `
                -VMName $vmName `
                -DomainName $contosoDomain `
                -LocalCredential $localCredential `
                -DomainAdministratorCredential $contosoAdminCredential
        }
    }

    ########################################################
    # PHASE 9 - PKI ROLE INSTALLATION ONLY
    ########################################################

    Write-Stage "PHASE 9 - PKI ROLE INSTALLATION ONLY"

    Ensure-WindowsFeatures `
        -VMName "RootCA" `
        -Credential $localCredential `
        -FeatureNames @("ADCS-Cert-Authority") `
        -IncludeManagementTools

    Ensure-WindowsFeatures `
        -VMName "ContSubCA" `
        -Credential $contosoAdminCredential `
        -FeatureNames @("ADCS-Cert-Authority") `
        -IncludeManagementTools

    Ensure-WindowsFeatures `
        -VMName "PKIOperations" `
        -Credential $contosoAdminCredential `
        -FeatureNames @(
            "Web-Server",
            "ADCS-Enroll-Web-Pol",
            "ADCS-Enroll-Web-Svc",
            "ADCS-Device-Enrollment"
        ) `
        -IncludeManagementTools

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

Build-Lab -Config $LabConfig
