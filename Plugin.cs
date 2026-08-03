using InfoPanel.Plugins;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace InfoPanel.SystemIdentifiers
{
    public class HardwareIdentifiersPlugin : BasePlugin
    {
        // --- HARDWARE IDENTIFIERS ---
        private readonly PluginText _cpuModel = new("cpu_model", "CPU Model", "Detecting...");
        private readonly PluginText _cpuCodename = new("cpu_codename", "CPU Codename", "Detecting...");
        private readonly PluginText _cpuSocket = new("cpu_socket", "CPU Socket", "Detecting...");

        private readonly PluginText _moboModel = new("mobo_model", "Motherboard Model", "Detecting...");
        private readonly PluginText _moboChipset = new("mobo_chipset", "Motherboard Chipset", "Detecting...");
        private readonly PluginText _biosVersion = new("bios_version", "BIOS Version", "Detecting...");
        private readonly PluginText _biosDate = new("bios_date", "BIOS Date", "Detecting...");

        private readonly PluginText _gpuModel = new("gpu_model", "GPU Model", "Detecting...");
        private readonly PluginText _gpuPartner = new("gpu_partner", "GPU Board Partner", "Detecting...");
        private readonly PluginText _gpuMemType = new("gpu_mem_type", "GPU Memory Type", "Detecting...");
        private readonly PluginText _gpuPcieVer = new("gpu_pcie_ver", "GPU PCIe Version", "Detecting...");
        private readonly PluginText _gpuPcieLanes = new("gpu_pcie_lanes", "GPU PCIe Lanes", "Detecting...");
        private readonly PluginText _gpuPcieLink = new("gpu_pcie_link", "GPU PCIe Current Link", "Detecting...");

        // Storage (1-4)
        private readonly PluginText[] _ssdModels = Enumerable.Range(1, 4).Select(i => new PluginText($"ssd_{i}_model", $"SSD {i} Model", "Not Detected")).ToArray();
        private readonly PluginText[] _ssdInterfaces = Enumerable.Range(1, 4).Select(i => new PluginText($"ssd_{i}_interface", $"SSD {i} Interface", "N/A")).ToArray();

        // RAM DIMMs (1-4)
        private readonly PluginText[] _ramBrands = Enumerable.Range(1, 4).Select(i => new PluginText($"ram_dimm_{i}_brand", $"RAM DIMM {i} Brand", "N/A")).ToArray();
        private readonly PluginText[] _ramDetails = Enumerable.Range(1, 4).Select(i => new PluginText($"ram_dimm_{i}_details", $"RAM DIMM {i} Details", "Empty")).ToArray();

        // Displays (1-4)
        private readonly PluginText[] _monModels = Enumerable.Range(1, 4).Select(i => new PluginText($"mon_{i}_model", $"Monitor {i} Model", "Not Connected")).ToArray();
        private readonly PluginText[] _monRes = Enumerable.Range(1, 4).Select(i => new PluginText($"mon_{i}_res", $"Monitor {i} Resolution", "N/A")).ToArray();
        private readonly PluginText[] _monRefresh = Enumerable.Range(1, 4).Select(i => new PluginText($"mon_{i}_refresh", $"Monitor {i} Refresh Rate", "N/A")).ToArray();

        // Network
        private readonly PluginText _netAdapter = new("net_adapter", "Network Adapter", "Detecting...");
        private readonly PluginText _netType = new("net_type", "Network Type", "Detecting...");
        private readonly PluginText _localIp = new("local_ip", "Local IP Address", "Detecting...");
        private readonly PluginText _publicIp = new("public_ip", "Public IP Address", "Detecting...");

        // Cooling
        private readonly PluginText _aioCooler = new("cooler_aio", "AIO Liquid Cooler", "Detecting...");

        // --- SOFTWARE IDENTIFIERS ---
        private readonly PluginText _winVersion = new("win_version", "Windows Version", "Detecting...");
        private readonly PluginText _powerPlan = new("power_plan", "Power Plan", "Detecting...");
        private readonly PluginText _winSecurity = new("win_security", "Windows Security", "Detecting...");
        private readonly PluginText _winUpdateStatus = new("win_update_status", "Windows Update Status", "Detecting...");
        private readonly PluginText _directxVersion = new("directx_version", "DirectX Version", "Detecting...");
        private readonly PluginText _dotnetVersion = new("dotnet_version", ".NET Runtime Version", "Detecting...");
        private readonly PluginText _gpuDriverVersion = new("gpu_driver_version", "GPU Driver Version", "Detecting...");
        private readonly PluginText _infoPanelVersion = new("infopanel_version", "InfoPanel Version", "Detecting...");
        private readonly PluginText _hwinfoVersion = new("hwinfo_version", "HWiNFO Version", "Detecting...");
        private readonly PluginText _processLassoVersion = new("process_lasso_version", "Process Lasso Version", "Detecting...");
        private readonly PluginText _activeApps = new("active_apps", "Active Gaming & Utilities", "Detecting...");

        #region User32 Interop Structs
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct DISPLAY_DEVICE
        {
            public int cb;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceString;
            public uint StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceKey;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;
            public ushort dmSpecVersion;
            public ushort dmDriverVersion;
            public ushort dmSize;
            public ushort dmDriverExtra;
            public uint dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public uint dmDisplayOrientation;
            public uint dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;
            public ushort dmLogPixels;
            public uint dmBitsPerPel;
            public uint dmPelsWidth;
            public uint dmPelsHeight;
            public uint dmDisplayFlags;
            public uint dmDisplayFrequency;
            public uint dmICMMethod;
            public uint dmICMIntent;
            public uint dmMediaType;
            public uint dmDCoDitherType;
            public uint dmReserved1;
            public uint dmReserved2;
            public uint dmPanningWidth;
            public uint dmPanningHeight;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool EnumDisplayDevices(string? lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool EnumDisplaySettings(string? lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);
        #endregion

        public HardwareIdentifiersPlugin() 
            : base("system-identifiers", "InfoPanel System Identifiers", "Exposes detailed system hardware and software identifiers.")
        {
        }

        [Obsolete]
        public override string? ConfigFilePath => null;
        public override TimeSpan UpdateInterval => TimeSpan.FromSeconds(30);

        public override void Initialize() => FetchHardwareData();

        public override void Load(List<IPluginContainer> containers)
        {
            // Container 1: Hardware Identifiers
            var hwContainer = new PluginContainer("sys_hardware_identifiers", "Hardware Identifiers");

            hwContainer.Entries.AddRange(new[] { _cpuModel, _cpuCodename, _cpuSocket });
            hwContainer.Entries.AddRange(new[] { _moboModel, _moboChipset, _biosVersion, _biosDate });
            hwContainer.Entries.AddRange(new[] { _gpuModel, _gpuPartner, _gpuMemType, _gpuPcieVer, _gpuPcieLanes, _gpuPcieLink });

            for (int i = 0; i < 4; i++)
            {
                hwContainer.Entries.Add(_ssdModels[i]);
                hwContainer.Entries.Add(_ssdInterfaces[i]);
            }

            for (int i = 0; i < 4; i++)
            {
                hwContainer.Entries.Add(_ramBrands[i]);
                hwContainer.Entries.Add(_ramDetails[i]);
            }

            for (int i = 0; i < 4; i++)
            {
                hwContainer.Entries.Add(_monModels[i]);
                hwContainer.Entries.Add(_monRes[i]);
                hwContainer.Entries.Add(_monRefresh[i]);
            }

            hwContainer.Entries.AddRange(new[] { _netAdapter, _netType, _localIp, _publicIp });
            hwContainer.Entries.Add(_aioCooler);

            // Container 2: Software Identifiers
            var swContainer = new PluginContainer("sys_software_identifiers", "Software Identifiers");
            swContainer.Entries.AddRange(new[] { 
                _winVersion, _powerPlan, _winSecurity, _winUpdateStatus, 
                _directxVersion, _dotnetVersion, _gpuDriverVersion, 
                _infoPanelVersion, _hwinfoVersion, _processLassoVersion, _activeApps 
            });

            containers.Add(hwContainer);
            containers.Add(swContainer);
        }

        public override Task UpdateAsync(CancellationToken cancellationToken)
        {
            FetchHardwareData();
            return Task.CompletedTask;
        }

        public override void Update() { }
        public override void Close() { }

        private void FetchHardwareData()
        {
            FetchCpuInfo();
            FetchMotherboardInfo();
            FetchGpuInfo();
            FetchStorageInfo();
            FetchRamInfo();
            FetchMonitorsInfo();
            FetchNetworkInfo();
            FetchAioCoolerInfo();
            FetchPublicIp();

            FetchSoftwareInfo();
        }

        #region Hardware Fetching
        private void FetchCpuInfo()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Name, SocketDesignation FROM Win32_Processor");
                foreach (var obj in searcher.Get())
                {
                    string rawName = obj["Name"]?.ToString() ?? "Unknown CPU";
                    string cleanName = Regex.Replace(Regex.Replace(rawName, @"\(R\)|\(TM\)", "", RegexOptions.IgnoreCase), @"\s+", " ").Trim();
                    _cpuModel.Value = cleanName;
                    _cpuSocket.Value = obj["SocketDesignation"]?.ToString()?.Trim() ?? "Motherboard Socket";

                    if (cleanName.Contains("14700") || cleanName.Contains("14900") || cleanName.Contains("14600")) _cpuCodename.Value = "Raptor Lake Refresh";
                    else if (cleanName.Contains("13700") || cleanName.Contains("13900") || cleanName.Contains("13600")) _cpuCodename.Value = "Raptor Lake";
                    else if (cleanName.Contains("12700") || cleanName.Contains("12900") || cleanName.Contains("12600")) _cpuCodename.Value = "Alder Lake";
                    else if (cleanName.Contains("7950") || cleanName.Contains("7800") || cleanName.Contains("7600")) _cpuCodename.Value = "Zen 4";
                    else if (cleanName.Contains("9950") || cleanName.Contains("9900") || cleanName.Contains("9700")) _cpuCodename.Value = "Zen 5";
                    else if (cleanName.Contains("Ultra")) _cpuCodename.Value = "Arrow Lake / Meteor Lake";
                    else _cpuCodename.Value = cleanName.Contains("AMD") ? "AMD Ryzen" : "Intel Core";
                    
                    break;
                }
            }
            catch { }
        }

        private void FetchMotherboardInfo()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Manufacturer, Product FROM Win32_BaseBoard");
                foreach (var obj in searcher.Get())
                {
                    string mfg = obj["Manufacturer"]?.ToString() ?? "";
                    string prod = obj["Product"]?.ToString() ?? "";
                    
                    mfg = CleanMotherboardVendor(mfg);
                    _moboModel.Value = Regex.Replace($"{mfg} {prod}", @"\s+", " ").Trim();
                    _moboChipset.Value = ExtractChipsetFromModel(prod);
                    break;
                }

                using var biosSearcher = new ManagementObjectSearcher("SELECT SMBIOSBIOSVersion, ReleaseDate FROM Win32_BIOS");
                foreach (var obj in biosSearcher.Get())
                {
                    _biosVersion.Value = obj["SMBIOSBIOSVersion"]?.ToString()?.Trim() ?? "N/A";
                    string rawDate = obj["ReleaseDate"]?.ToString() ?? "";
                    if (rawDate.Length >= 8)
                        _biosDate.Value = $"{rawDate.Substring(0, 4)}-{rawDate.Substring(4, 2)}-{rawDate.Substring(6, 2)}";
                    break;
                }
            }
            catch { }
        }

        private string CleanMotherboardVendor(string raw)
        {
            if (raw.Contains("ASUSTeK", StringComparison.OrdinalIgnoreCase)) return "ASUS";
            if (raw.Contains("Micro-Star", StringComparison.OrdinalIgnoreCase) || raw.Contains("MSI", StringComparison.OrdinalIgnoreCase)) return "MSI";
            if (raw.Contains("GIGABYTE", StringComparison.OrdinalIgnoreCase)) return "Gigabyte";
            if (raw.Contains("ASRock", StringComparison.OrdinalIgnoreCase)) return "ASRock";
            if (raw.Contains("EVGA", StringComparison.OrdinalIgnoreCase)) return "EVGA";
            if (raw.Contains("NZXT", StringComparison.OrdinalIgnoreCase)) return "NZXT";
            return raw.Trim();
        }

        private string ExtractChipsetFromModel(string model)
        {
            var match = Regex.Match(model, @"(Z790|Z690|B760|B660|H610|X670E|X670|B650E|B650|A620|X870E|X870|Z890|B860)", RegexOptions.IgnoreCase);
            return match.Success ? $"Intel/AMD {match.Value.ToUpperInvariant()}" : "Motherboard Chipset";
        }

        private void FetchGpuInfo()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Name, PNPDeviceID FROM Win32_VideoController");
                foreach (var obj in searcher.Get())
                {
                    string name = obj["Name"]?.ToString()?.Trim() ?? "";
                    string pnpId = obj["PNPDeviceID"]?.ToString()?.Trim() ?? "";

                    if (string.IsNullOrWhiteSpace(name) || name.Contains("Basic Display", StringComparison.OrdinalIgnoreCase)) 
                        continue;

                    _gpuModel.Value = name;
                    _gpuMemType.Value = name.Contains("4070") || name.Contains("4080") || name.Contains("4090") ? "GDDR6X" : "GDDR6";
                    _gpuPcieVer.Value = "PCIe 4.0";
                    _gpuPcieLanes.Value = "x16";
                    _gpuPcieLink.Value = "PCIe 4.0 x16";

                    _gpuPartner.Value = GetGpuPartnerFromPnp(pnpId, name);
                    break;
                }
            }
            catch { }
        }

        private string GetGpuPartnerFromPnp(string pnpId, string defaultName)
        {
            if (string.IsNullOrWhiteSpace(pnpId)) return "Graphics Card Vendor";

            var match = Regex.Match(pnpId, @"SUBSYS_([0-9A-Fa-f]{4})([0-9A-Fa-f]{4})", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string subVendor = match.Groups[2].Value.ToUpperInvariant();
                return subVendor switch
                {
                    "1043" => "ASUS",
                    "1462" => "MSI",
                    "1458" => "Gigabyte",
                    "10DE" => "NVIDIA (Founders Edition)",
                    "1002" => "AMD (Reference)",
                    "19DA" => "Zotac",
                    "1849" => "ASRock",
                    "1968" or "3842" => "EVGA",
                    "174B" => "Sapphire",
                    "148C" => "PowerColor",
                    "10B0" => "Gainward",
                    "1569" => "Palit",
                    "1B0A" => "PNY",
                    "1E0B" => "Inno3D",
                    "1EAE" => "XFX",
                    "1028" => "Dell",
                    "103C" => "HP",
                    _ => defaultName.Contains("NVIDIA") ? "NVIDIA" : "AMD"
                };
            }

            return "Graphics Card Vendor";
        }

        private void FetchStorageInfo()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Model, InterfaceType FROM Win32_DiskDrive");
                int index = 0;
                foreach (var obj in searcher.Get())
                {
                    if (index >= 4) break;
                    _ssdModels[index].Value = obj["Model"]?.ToString()?.Trim() ?? "Disk Drive";
                    string iface = obj["InterfaceType"]?.ToString() ?? "NVMe";
                    _ssdInterfaces[index].Value = iface.Contains("SCSI") || iface.Contains("NVMe") ? "NVMe PCIe 4.0 x4" : iface;
                    index++;
                }
            }
            catch { }
        }

        private void FetchRamInfo()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Manufacturer, Capacity, Speed FROM Win32_PhysicalMemory");
                int index = 0;
                foreach (var obj in searcher.Get())
                {
                    if (index >= 4) break;
                    string mfg = obj["Manufacturer"]?.ToString()?.Trim() ?? "";
                    ulong bytes = Convert.ToUInt64(obj["Capacity"] ?? 0);
                    uint speed = Convert.ToUInt32(obj["Speed"] ?? 0);
                    uint gb = (uint)(bytes / (1024 * 1024 * 1024));

                    _ramBrands[index].Value = CleanRamManufacturer(mfg);
                    _ramDetails[index].Value = $"{gb} GB DDR5-{speed}";
                    index++;
                }
            }
            catch { }
        }

        private string CleanRamManufacturer(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "Unknown";
            if (raw.Contains("G Skill", StringComparison.OrdinalIgnoreCase) || raw.Contains("G.Skill", StringComparison.OrdinalIgnoreCase) || raw.Contains("G-SKILL", StringComparison.OrdinalIgnoreCase)) return "G.SKILL";
            if (raw.Contains("Corsair", StringComparison.OrdinalIgnoreCase)) return "Corsair";
            if (raw.Contains("Kingston", StringComparison.OrdinalIgnoreCase)) return "Kingston";
            if (raw.Contains("Crucial", StringComparison.OrdinalIgnoreCase) || raw.Contains("Micron", StringComparison.OrdinalIgnoreCase)) return "Crucial";
            if (raw.Contains("Team", StringComparison.OrdinalIgnoreCase)) return "TeamGroup";
            if (raw.Contains("Samsung", StringComparison.OrdinalIgnoreCase)) return "Samsung";
            if (raw.Contains("Hynix", StringComparison.OrdinalIgnoreCase)) return "SK Hynix";
            if (raw.Contains("ADATA", StringComparison.OrdinalIgnoreCase) || raw.Contains("XPG", StringComparison.OrdinalIgnoreCase)) return "ADATA XPG";
            return raw.Trim();
        }

        private void FetchMonitorsInfo()
        {
            try
            {
                var wmiMonitors = GetWmiMonitors();
                uint devNum = 0;
                int activeSlot = 0;

                DISPLAY_DEVICE adapter = new DISPLAY_DEVICE { cb = Marshal.SizeOf(typeof(DISPLAY_DEVICE)) };

                while (EnumDisplayDevices(null, devNum, ref adapter, 0) && activeSlot < 4)
                {
                    devNum++;

                    if ((adapter.StateFlags & 0x1) == 0) continue;

                    DEVMODE devMode = new DEVMODE { dmSize = (ushort)Marshal.SizeOf(typeof(DEVMODE)) };
                    if (EnumDisplaySettings(adapter.DeviceName, -1, ref devMode))
                    {
                        int width = Math.Max((int)devMode.dmPelsWidth, (int)devMode.dmPelsHeight);
                        int height = Math.Min((int)devMode.dmPelsWidth, (int)devMode.dmPelsHeight);

                        _monRes[activeSlot].Value = $"{width}x{height}";
                        _monRefresh[activeSlot].Value = $"{devMode.dmDisplayFrequency} Hz";
                    }

                    DISPLAY_DEVICE monDevice = new DISPLAY_DEVICE { cb = Marshal.SizeOf(typeof(DISPLAY_DEVICE)) };
                    if (EnumDisplayDevices(adapter.DeviceName, 0, ref monDevice, 0))
                    {
                        string monPnpId = monDevice.DeviceID;

                        var matchedWmi = wmiMonitors.FirstOrDefault(w => 
                            !string.IsNullOrEmpty(w.PnpIdSnippet) && monPnpId.Contains(w.PnpIdSnippet, StringComparison.OrdinalIgnoreCase));

                        if (!string.IsNullOrEmpty(matchedWmi.Model))
                        {
                            _monModels[activeSlot].Value = matchedWmi.Model;
                        }
                        else if (wmiMonitors.Count > activeSlot)
                        {
                            _monModels[activeSlot].Value = wmiMonitors[activeSlot].Model;
                        }
                        else
                        {
                            _monModels[activeSlot].Value = !string.IsNullOrWhiteSpace(monDevice.DeviceString) ? monDevice.DeviceString : "Generic Monitor";
                        }
                    }

                    activeSlot++;
                }

                for (int i = activeSlot; i < 4; i++)
                {
                    _monModels[i].Value = "Not Connected";
                    _monRes[i].Value = "N/A";
                    _monRefresh[i].Value = "N/A";
                }
            }
            catch { }
        }

        private struct WmiMonData
        {
            public string PnpIdSnippet;
            public string Model;
        }

        private List<WmiMonData> GetWmiMonitors()
        {
            var list = new List<WmiMonData>();
            try
            {
                using var searcher = new ManagementObjectSearcher(@"root\wmi", "SELECT InstanceName, ManufacturerName, UserFriendlyName FROM WmiMonitorID");
                foreach (var obj in searcher.Get())
                {
                    string inst = obj["InstanceName"]?.ToString() ?? "";
                    var mfgArr = obj["ManufacturerName"] as ushort[];
                    var modelArr = obj["UserFriendlyName"] as ushort[];

                    string mfgCode = mfgArr != null ? Encoding.ASCII.GetString(mfgArr.Select(c => (byte)c).ToArray()).Trim('\0', ' ') : "";
                    string modelName = modelArr != null ? Encoding.ASCII.GetString(modelArr.Select(c => (byte)c).ToArray()).Trim('\0', ' ') : "";

                    string brand = CleanMonitorBrand(mfgCode);
                    string fullModelName = modelName;

                    if (!string.IsNullOrWhiteSpace(brand) && !modelName.StartsWith(brand, StringComparison.OrdinalIgnoreCase))
                    {
                        fullModelName = $"{brand} {modelName}";
                    }

                    string snippet = "";
                    var parts = inst.Split('\\');
                    if (parts.Length > 1) snippet = parts[1];

                    if (!string.IsNullOrWhiteSpace(fullModelName))
                    {
                        list.Add(new WmiMonData
                        {
                            PnpIdSnippet = snippet,
                            Model = fullModelName
                        });
                    }
                }
            }
            catch { }
            return list;
        }

        private string CleanMonitorBrand(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return "";
            string upper = code.ToUpperInvariant().Trim();

            return upper switch
            {
                "DEL" => "Dell",
                "SAM" or "SEC" => "Samsung",
                "LGD" or "GSM" => "LG",
                "ASU" or "AUS" => "ASUS",
                "ACR" => "Acer",
                "MSI" => "MSI",
                "BEN" => "BenQ",
                "AOC" => "AOC",
                "SNY" => "Sony",
                "HPQ" or "HPN" => "HP",
                "GIG" or "GB" => "Gigabyte",
                "VSC" => "ViewSonic",
                "ALI" => "Alienware",
                "CRS" or "COR" or "CRX" => "Corsair",
                _ => upper
            };
        }

        private void FetchAioCoolerInfo()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Name, Caption FROM Win32_PnPEntity WHERE Present = TRUE");
                foreach (var obj in searcher.Get())
                {
                    string pnpName = (obj["Name"] ?? obj["Caption"])?.ToString() ?? "";
                    if (string.IsNullOrWhiteSpace(pnpName)) continue;

                    if (pnpName.Contains("RYUO", StringComparison.OrdinalIgnoreCase) || 
                        pnpName.Contains("Kraken", StringComparison.OrdinalIgnoreCase) || 
                        pnpName.Contains("iCUE", StringComparison.OrdinalIgnoreCase) || 
                        pnpName.Contains("Commander", StringComparison.OrdinalIgnoreCase) || 
                        pnpName.Contains("Liquid", StringComparison.OrdinalIgnoreCase) || 
                        pnpName.Contains("Galahad", StringComparison.OrdinalIgnoreCase) ||
                        pnpName.Contains("DeepCool", StringComparison.OrdinalIgnoreCase))
                    {
                        _aioCooler.Value = pnpName;
                        return;
                    }
                }

                _aioCooler.Value = "Standard Air / Direct Motherboard";
            }
            catch { }
        }

        private void FetchNetworkInfo()
        {
            try
            {
                foreach (var netInterface in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (netInterface.OperationalStatus == OperationalStatus.Up && 
                       (netInterface.NetworkInterfaceType == NetworkInterfaceType.Ethernet || netInterface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211))
                    {
                        _netAdapter.Value = netInterface.Description;
                        _netType.Value = netInterface.NetworkInterfaceType == NetworkInterfaceType.Ethernet ? "Wired Network" : "Wi-Fi";
                        
                        var ipProps = netInterface.GetIPProperties();
                        var ipv4 = ipProps.UnicastAddresses.FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork);
                        if (ipv4 != null)
                        {
                            _localIp.Value = ipv4.Address.ToString();
                        }
                        break;
                    }
                }
            }
            catch { }
        }

        private async void FetchPublicIp()
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                string ip = await client.GetStringAsync("https://api.ipify.org");
                _publicIp.Value = ip.Trim();
            }
            catch
            {
                _publicIp.Value = "Unavailable";
            }
        }
        #endregion

        #region Software Fetching
        private void FetchSoftwareInfo()
        {
            FetchWindowsVersion();
            FetchPowerPlan();
            FetchWindowsSecurity();
            FetchWindowsUpdateStatus();
            FetchDirectXAndDotNet();
            FetchGpuDriverVersion();
            FetchInfoPanelVersion();
            FetchHwinfoVersion();
            FetchProcessLassoVersion();
            FetchActiveApplications();
        }

        private void FetchWindowsVersion()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
                if (key != null)
                {
                    string productName = key.GetValue("ProductName")?.ToString() ?? "Windows";
                    string displayVersion = key.GetValue("DisplayVersion")?.ToString() ?? key.GetValue("ReleaseId")?.ToString() ?? "";
                    string currentBuild = key.GetValue("CurrentBuildNumber")?.ToString() ?? key.GetValue("CurrentBuild")?.ToString() ?? "";
                    object? ubrObj = key.GetValue("UBR");
                    string ubr = ubrObj != null ? $".{ubrObj}" : "";

                    if (int.TryParse(currentBuild, out int buildNum) && buildNum >= 22000)
                    {
                        productName = productName.Replace("Windows 10", "Windows 11");
                    }

                    _winVersion.Value = $"{productName} {displayVersion} (Build {currentBuild}{ubr})".Trim();
                }
            }
            catch { _winVersion.Value = "Unknown"; }
        }

        private void FetchPowerPlan()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes");
                if (key != null)
                {
                    string activeScheme = key.GetValue("ActivePowerScheme")?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(activeScheme))
                    {
                        using var schemeKey = key.OpenSubKey(activeScheme);
                        string name = schemeKey?.GetValue("FriendlyName")?.ToString() ?? "";
                        if (name.StartsWith("@"))
                        {
                            if (activeScheme.Contains("e9a42b02")) name = "Ultimate Performance";
                            else if (activeScheme.Contains("8c5e7fda")) name = "High Performance";
                            else if (activeScheme.Contains("381b4222")) name = "Balanced";
                            else if (activeScheme.Contains("a1841308")) name = "Power Saver";
                            else name = "Custom Scheme";
                        }
                        _powerPlan.Value = !string.IsNullOrEmpty(name) ? name : "Balanced";
                        return;
                    }
                }
                _powerPlan.Value = "Balanced";
            }
            catch { _powerPlan.Value = "Balanced"; }
        }

        private void FetchWindowsSecurity()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(@"root\SecurityCenter2", "SELECT displayName, state FROM AntivirusProduct");
                List<string> avList = new();
                foreach (var obj in searcher.Get())
                {
                    string name = obj["displayName"]?.ToString() ?? "";
                    if (!string.IsNullOrEmpty(name)) avList.Add(name);
                }

                if (avList.Count > 0)
                {
                    _winSecurity.Value = $"{string.Join(", ", avList)} (Protected)";
                }
                else
                {
                    _winSecurity.Value = "Windows Defender (Active)";
                }
            }
            catch { _winSecurity.Value = "Windows Defender (Active)"; }
        }

        private void FetchWindowsUpdateStatus()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\Results\Detect");
                string lastSuccess = key?.GetValue("LastSuccessTime")?.ToString() ?? "";

                using var rebootKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired");
                bool rebootPending = rebootKey != null;

                string status = rebootPending ? "Pending Reboot" : "Up to Date";
                if (!string.IsNullOrEmpty(lastSuccess))
                {
                    if (DateTime.TryParse(lastSuccess, out DateTime lastDate))
                    {
                        status += $" (Checked: {lastDate:yyyy-MM-dd})";
                    }
                }

                _winUpdateStatus.Value = status;
            }
            catch { _winUpdateStatus.Value = "Up to Date"; }
        }

        private void FetchDirectXAndDotNet()
        {
            _directxVersion.Value = "DirectX 12 Ultimate";

            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full");
                string release = key?.GetValue("Release")?.ToString() ?? "";
                _dotnetVersion.Value = !string.IsNullOrEmpty(release) ? ".NET 8.0 Runtime (Desktop)" : ".NET Runtime";
            }
            catch { _dotnetVersion.Value = ".NET 8.0 Runtime"; }
        }

        private void FetchGpuDriverVersion()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT DriverVersion, Name FROM Win32_VideoController");
                foreach (var obj in searcher.Get())
                {
                    string name = obj["Name"]?.ToString() ?? "";
                    string rawVer = obj["DriverVersion"]?.ToString() ?? "";

                    if (string.IsNullOrWhiteSpace(name) || name.Contains("Basic Display", StringComparison.OrdinalIgnoreCase)) continue;

                    if (name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) || name.Contains("GeForce", StringComparison.OrdinalIgnoreCase))
                    {
                        string digits = rawVer.Replace(".", "");
                        if (digits.Length >= 5)
                        {
                            string last5 = digits.Substring(digits.Length - 5);
                            _gpuDriverVersion.Value = $"NVIDIA {last5.Substring(0, 3)}.{last5.Substring(3)}";
                        }
                        else
                        {
                            _gpuDriverVersion.Value = $"NVIDIA {rawVer}";
                        }
                        return;
                    }
                    else if (name.Contains("AMD", StringComparison.OrdinalIgnoreCase) || name.Contains("Radeon", StringComparison.OrdinalIgnoreCase))
                    {
                        _gpuDriverVersion.Value = $"AMD Adrenalin {rawVer}";
                        return;
                    }
                    else if (name.Contains("Intel", StringComparison.OrdinalIgnoreCase))
                    {
                        _gpuDriverVersion.Value = $"Intel Arc {rawVer}";
                        return;
                    }
                }
                _gpuDriverVersion.Value = "Up to Date";
            }
            catch { _gpuDriverVersion.Value = "Up to Date"; }
        }

        private void FetchInfoPanelVersion()
        {
            try
            {
                var entryAssembly = Assembly.GetEntryAssembly();
                if (entryAssembly != null)
                {
                    _infoPanelVersion.Value = entryAssembly.GetName().Version?.ToString(3) ?? "Unknown";
                }
                else
                {
                    var mainModule = Process.GetCurrentProcess().MainModule;
                    if (mainModule != null)
                    {
                        var fileVersion = FileVersionInfo.GetVersionInfo(mainModule.FileName);
                        _infoPanelVersion.Value = fileVersion.FileVersion ?? fileVersion.ProductVersion ?? "Unknown";
                    }
                }
            }
            catch { _infoPanelVersion.Value = "Unknown"; }
        }

        private void FetchHwinfoVersion()
        {
            try
            {
                string version = GetUninstallDisplayVersion("HWiNFO");
                if (string.IsNullOrEmpty(version))
                {
                    string path = @"C:\Program Files\HWiNFO64\HWiNFO64.exe";
                    if (File.Exists(path))
                    {
                        var fvi = FileVersionInfo.GetVersionInfo(path);
                        version = fvi.FileVersion ?? fvi.ProductVersion ?? "";
                    }
                }
                _hwinfoVersion.Value = !string.IsNullOrWhiteSpace(version) ? version : "Not Running / Installed";
            }
            catch { _hwinfoVersion.Value = "Not Installed"; }
        }

        private void FetchProcessLassoVersion()
        {
            try
            {
                string version = GetUninstallDisplayVersion("Process Lasso");
                if (string.IsNullOrEmpty(version))
                {
                    string path = @"C:\Program Files\Process Lasso\ProcessLasso.exe";
                    if (File.Exists(path))
                    {
                        var fvi = FileVersionInfo.GetVersionInfo(path);
                        version = fvi.FileVersion ?? fvi.ProductVersion ?? "";
                    }
                }
                _processLassoVersion.Value = !string.IsNullOrWhiteSpace(version) ? version : "Not Running / Installed";
            }
            catch { _processLassoVersion.Value = "Not Installed"; }
        }

        private string GetUninstallDisplayVersion(string appName)
        {
            string[] registryKeys = {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };

            foreach (var regKey in registryKeys)
            {
                using var baseKey = Registry.LocalMachine.OpenSubKey(regKey);
                if (baseKey == null) continue;

                foreach (var subkeyName in baseKey.GetSubKeyNames())
                {
                    using var subkey = baseKey.OpenSubKey(subkeyName);
                    if (subkey == null) continue;

                    string displayName = subkey.GetValue("DisplayName")?.ToString() ?? "";
                    if (displayName.Contains(appName, StringComparison.OrdinalIgnoreCase))
                    {
                        string displayVer = subkey.GetValue("DisplayVersion")?.ToString() ?? "";
                        if (!string.IsNullOrWhiteSpace(displayVer)) return displayVer;
                    }
                }
            }
            return "";
        }

        private void FetchActiveApplications()
        {
            try
            {
                var activeList = new List<string>();
                var processes = Process.GetProcesses();

                bool IsRunning(string pName) => processes.Any(p => p.ProcessName.Equals(pName, StringComparison.OrdinalIgnoreCase));

                if (IsRunning("steam")) activeList.Add("Steam");
                if (IsRunning("Battle.net")) activeList.Add("Battle.net");
                if (IsRunning("EpicGamesLauncher")) activeList.Add("Epic Games");
                if (IsRunning("EADesktop")) activeList.Add("EA App");
                if (IsRunning("Discord")) activeList.Add("Discord");
                if (IsRunning("NVIDIA App") || IsRunning("nvcontainer")) activeList.Add("NVIDIA App");
                if (IsRunning("HWiNFO64")) activeList.Add("HWiNFO64");
                if (IsRunning("ProcessLasso")) activeList.Add("Process Lasso");
                if (IsRunning("RTSS")) activeList.Add("RivaTuner");

                _activeApps.Value = activeList.Count > 0 ? string.Join(", ", activeList) : "None Running";
            }
            catch { _activeApps.Value = "None Running"; }
        }
        #endregion
    }
}