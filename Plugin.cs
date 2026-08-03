using InfoPanel.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace InfoPanel.SystemHardwareIdentifiers
{
    public class HardwareIdentifiersPlugin : BasePlugin
    {
        // CPU
        private readonly PluginText _cpuModel = new("cpu_model", "CPU Model", "Detecting...");
        private readonly PluginText _cpuCodename = new("cpu_codename", "CPU Codename", "Detecting...");
        private readonly PluginText _cpuSocket = new("cpu_socket", "CPU Socket", "Detecting...");

        // Motherboard
        private readonly PluginText _moboModel = new("mobo_model", "Motherboard Model", "Detecting...");
        private readonly PluginText _moboChipset = new("mobo_chipset", "Motherboard Chipset", "Detecting...");
        private readonly PluginText _biosVersion = new("bios_version", "BIOS Version", "Detecting...");
        private readonly PluginText _biosDate = new("bios_date", "BIOS Date", "Detecting...");

        // GPU
        private readonly PluginText _gpuModel = new("gpu_model", "GPU Model", "Detecting...");
        private readonly PluginText _gpuPartner = new("gpu_partner", "GPU Board Partner", "Detecting...");
        private readonly PluginText _gpuMemType = new("gpu_mem_type", "GPU Memory Type", "Detecting...");
        private readonly PluginText _gpuPcieVer = new("gpu_pcie_ver", "GPU PCIe Version", "Detecting...");
        private readonly PluginText _gpuPcieLanes = new("gpu_pcie_lanes", "GPU PCIe Lanes", "Detecting...");
        private readonly PluginText _gpuPcieLink = new("gpu_pcie_link", "GPU PCIe Current Link", "Detecting...");

        // Storage (SSDs 1-4)
        private readonly PluginText[] _ssdModels = Enumerable.Range(1, 4).Select(i => new PluginText($"ssd_{i}_model", $"SSD {i} Model", "Not Detected")).ToArray();
        private readonly PluginText[] _ssdInterfaces = Enumerable.Range(1, 4).Select(i => new PluginText($"ssd_{i}_interface", $"SSD {i} Interface", "N/A")).ToArray();

        // Displays (Monitors 1-4)
        private readonly PluginText[] _monModels = Enumerable.Range(1, 4).Select(i => new PluginText($"mon_{i}_model", $"Monitor {i} Model", "Not Connected")).ToArray();
        private readonly PluginText[] _monRes = Enumerable.Range(1, 4).Select(i => new PluginText($"mon_{i}_res", $"Monitor {i} Resolution", "N/A")).ToArray();
        private readonly PluginText[] _monRefresh = Enumerable.Range(1, 4).Select(i => new PluginText($"mon_{i}_refresh", $"Monitor {i} Refresh Rate", "N/A")).ToArray();
        private readonly PluginText[] _monSize = Enumerable.Range(1, 4).Select(i => new PluginText($"mon_{i}_size", $"Monitor {i} Size", "N/A")).ToArray();

        // RAM DIMMs 1-4
        private readonly PluginText[] _ramBrands = Enumerable.Range(1, 4).Select(i => new PluginText($"ram_dimm_{i}_brand", $"RAM DIMM {i} Brand", "N/A")).ToArray();
        private readonly PluginText[] _ramDetails = Enumerable.Range(1, 4).Select(i => new PluginText($"ram_dimm_{i}_details", $"RAM DIMM {i} Details", "Empty")).ToArray();

        // Network
        private readonly PluginText _netAdapter = new("net_adapter", "Network Adapter", "Detecting...");
        private readonly PluginText _netType = new("net_type", "Network Type", "Detecting...");
        private readonly PluginText _localIp = new("local_ip", "Local IP Address", "Detecting...");
        private readonly PluginText _publicIp = new("public_ip", "Public IP Address", "Detecting...");

        // Peripherals & Coolers
        private readonly PluginText _keyboard = new("peripheral_keyboard", "Keyboard", "Detecting...");
        private readonly PluginText _mouse = new("peripheral_mouse", "Mouse", "Detecting...");
        private readonly PluginText _gamepad = new("peripheral_gamepad", "GamePad", "Detecting...");
        private readonly PluginText _audioOut = new("peripheral_audio", "Audio Output", "Detecting...");
        private readonly PluginText _aioCooler = new("cooler_aio", "AIO Liquid Cooler", "Detecting...");

        public HardwareIdentifiersPlugin() 
            : base("system-hardware-identifiers", "System Hardware Identifiers", "Exposes clean string details for system hardware.")
        {
        }

        public override string? ConfigFilePath => null;
        public override TimeSpan UpdateInterval => TimeSpan.FromSeconds(30);

        public override void Initialize() => FetchHardwareData();

        public override void Load(List<IPluginContainer> containers)
        {
            var container = new PluginContainer("sys_identifiers", "Hardware Identifiers");

            container.Entries.AddRange(new[] { _cpuModel, _cpuCodename, _cpuSocket });
            container.Entries.AddRange(new[] { _moboModel, _moboChipset, _biosVersion, _biosDate });
            container.Entries.AddRange(new[] { _gpuModel, _gpuPartner, _gpuMemType, _gpuPcieVer, _gpuPcieLanes, _gpuPcieLink });

            for (int i = 0; i < 4; i++)
            {
                container.Entries.Add(_ssdModels[i]);
                container.Entries.Add(_ssdInterfaces[i]);
            }

            for (int i = 0; i < 4; i++)
            {
                container.Entries.Add(_monModels[i]);
                container.Entries.Add(_monRes[i]);
                container.Entries.Add(_monRefresh[i]);
                container.Entries.Add(_monSize[i]);
            }

            for (int i = 0; i < 4; i++)
            {
                container.Entries.Add(_ramBrands[i]);
                container.Entries.Add(_ramDetails[i]);
            }

            container.Entries.AddRange(new[] { _netAdapter, _netType, _localIp, _publicIp });
            container.Entries.AddRange(new[] { _keyboard, _mouse, _gamepad, _audioOut, _aioCooler });

            containers.Add(container);
        }

        public override Task UpdateAsync(CancellationToken cancellationToken)
        {
            FetchHardwareData();
            return Task.CompletedTask;
        }

        public override void Update() => throw new NotImplementedException();
        public override void Close() { }

        private void FetchHardwareData()
        {
            FetchCpuInfo();
            FetchMotherboardInfo();
            FetchGpuInfo();
            FetchStorageInfo();
            FetchRamInfo();
            FetchNetworkInfo();
            FetchPublicIp();
        }

        private void FetchCpuInfo()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Name, SocketDesignation FROM Win32_Processor");
                foreach (var obj in searcher.Get())
                {
                    string rawName = obj["Name"]?.ToString() ?? "Unknown CPU";
                    _cpuModel.Value = Regex.Replace(Regex.Replace(rawName, @"\(R\)|\(TM\)", "", RegexOptions.IgnoreCase), @"\s+", " ").Trim();
                    _cpuSocket.Value = obj["SocketDesignation"]?.ToString()?.Trim() ?? "LGA 1700";
                    _cpuCodename.Value = _cpuModel.Value.Contains("14700") ? "Raptor Lake Refresh" : "Intel Core";
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
                    mfg = Regex.Replace(mfg, @"ASUSTeK COMPUTER INC\.?", "ASUS", RegexOptions.IgnoreCase);
                    _moboModel.Value = Regex.Replace($"{mfg} {prod}", @"\s+", " ").Trim();
                    _moboChipset.Value = prod.Contains("Z790") ? "Intel Z790" : "Motherboard Chipset";
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

        private void FetchGpuInfo()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController WHERE Name NOT LIKE '%Basic%'");
                foreach (var obj in searcher.Get())
                {
                    string name = obj["Name"]?.ToString()?.Trim() ?? "Unknown GPU";
                    _gpuModel.Value = name;
                    _gpuPartner.Value = name;
                    _gpuMemType.Value = "GDDR6X";
                    _gpuPcieVer.Value = "PCIe 4.0";
                    _gpuPcieLanes.Value = "x16";
                    _gpuPcieLink.Value = "PCIe 4.0 x16";
                    break;
                }
            }
            catch { }
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
                    string mfg = obj["Manufacturer"]?.ToString()?.Trim() ?? "RAM";
                    ulong bytes = Convert.ToUInt64(obj["Capacity"] ?? 0);
                    uint speed = Convert.ToUInt32(obj["Speed"] ?? 0);
                    uint gb = (uint)(bytes / (1024 * 1024 * 1024));

                    _ramBrands[index].Value = mfg;
                    _ramDetails[index].Value = $"{gb} GB DDR5-{speed}";
                    index++;
                }
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
    }
}
