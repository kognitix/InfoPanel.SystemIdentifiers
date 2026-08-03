using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using Microsoft.Win32;
using InfoPanel.Plugins;

namespace InfoPanel.SystemHardwareIdentifiers
{
    public class HardwareIdentifiersPlugin : BasePlugin
    {
        public override string Name => "System Hardware Identifiers";
        public override string Author => "kognitix";
        public override string Version => "1.0.0";

        private readonly List<PluginSensor> _sensors = new List<PluginSensor>();

        public override void Initialize()
        {
            _sensors.Clear();

            // Core Hardware
            AddSensor("CPU Model", GetCpuModel());
            AddSensor("Motherboard Model", GetMotherboardModel());
            AddSensor("GPU Model", GetGpuModel());

            // Peripherals & Input Devices
            AddSensor("Keyboard", GetPeripheralName("Keyboard", new[] { "HID Keyboard Device", "Standard PS/2 Keyboard" }));
            AddSensor("Mouse", GetPeripheralName("Mouse", new[] { "HID-compliant mouse" }));
            AddSensor("GamePad", GetGamePadName());

            // Cooling & Accessories
            AddSensor("AIO Liquid Cooler", GetUsbDeviceName(new[] { "RYUO", "Kraken", "Commander", "Galahad", "Liquid", "Cooler", "AIO" }));
            AddSensor("LED Controller", GetUsbDeviceName(new[] { "AURA", "Lighting", "RGB", "LED Controller" }));

            // Audio Devices & Headsets
            AddSensor("Audio Output (Active)", GetActiveAudioDevice(isCapture: false));
            AddSensor("Audio Input (Active)", GetActiveAudioDevice(isCapture: true));
            AddSensor("Bluetooth Headset", GetBluetoothAudioDevice());

            // Monitors
            DetectMonitors();
        }

        public override List<PluginSensor> GetSensors()
        {
            return _sensors;
        }

        private void AddSensor(string name, string value)
        {
            _sensors.Add(new PluginSensor
            {
                Name = name,
                Value = string.IsNullOrWhiteSpace(value) ? "N/A" : value
            });
        }

        // --- Hardware Query Methods ---

        private string GetCpuModel()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        return obj["Name"]?.ToString()?.Trim() ?? "N/A";
                    }
                }
            }
            catch { }
            return "N/A";
        }

        private string GetMotherboardModel()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Manufacturer, Product FROM Win32_BaseBoard"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        string mfr = obj["Manufacturer"]?.ToString()?.Trim();
                        string prod = obj["Product"]?.ToString()?.Trim();
                        return $"{mfr} {prod}".Trim();
                    }
                }
            }
            catch { }
            return "N/A";
        }

        private string GetGpuModel()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        string name = obj["Name"]?.ToString()?.Trim();
                        if (!string.IsNullOrEmpty(name) && !name.Contains("Basic Display"))
                        {
                            return name;
                        }
                    }
                }
            }
            catch { }
            return "N/A";
        }

        private string GetPeripheralName(string pnpClass, string[] ignoreFilters)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher($"SELECT Name, Caption FROM Win32_PnPEntity WHERE PNPClass = '{pnpClass}'"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        string name = (obj["Name"] ?? obj["Caption"])?.ToString()?.Trim();
                        if (!string.IsNullOrEmpty(name) && !ignoreFilters.Any(filter => name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0))
                        {
                            return name;
                        }
                    }
                }
            }
            catch { }
            return "N/A";
        }

        private string GetGamePadName()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Name, Caption FROM Win32_PnPEntity WHERE PNPClass IN ('XnaComposite', 'XboxPeripheral') OR Name LIKE '%Xbox%' OR Name LIKE '%Controller%'"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        string name = (obj["Name"] ?? obj["Caption"])?.ToString()?.Trim();
                        if (!string.IsNullOrEmpty(name) && !name.Contains("Root") && !name.Contains("Virtual"))
                        {
                            return name;
                        }
                    }
                }
            }
            catch { }
            return "N/A";
        }

        private string GetUsbDeviceName(string[] keywords)
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Name, Caption FROM Win32_PnPEntity WHERE PNPClass IN ('USBDevice', 'USB', 'HIDClass')"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        string name = (obj["Name"] ?? obj["Caption"])?.ToString()?.Trim();
                        if (!string.IsNullOrEmpty(name) && keywords.Any(kw => name.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0))
                        {
                            return name;
                        }
                    }
                }
            }
            catch { }
            return "N/A";
        }

        private string GetActiveAudioDevice(bool isCapture)
        {
            string keyPath = isCapture
                ? @"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Capture"
                : @"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render";

            try
            {
                using (RegistryKey baseKey = Registry.LocalMachine.OpenSubKey(keyPath))
                {
                    if (baseKey == null) return "N/A";

                    foreach (string subkeyName in baseKey.GetSubKeyNames())
                    {
                        using (RegistryKey deviceKey = baseKey.OpenSubKey(subkeyName))
                        {
                            if (deviceKey == null) continue;

                            int state = Convert.ToInt32(deviceKey.GetValue("DeviceState", 0));
                            if (state == 1) // Active default endpoint
                            {
                                using (RegistryKey propsKey = deviceKey.OpenSubKey("Properties"))
                                {
                                    if (propsKey != null)
                                    {
                                        string deviceName = propsKey.GetValue("{a45c254e-df1c-4efd-8020-67d146a850e0},2")?.ToString();
                                        if (!string.IsNullOrEmpty(deviceName))
                                        {
                                            return deviceName;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            return "N/A";
        }

        private string GetBluetoothAudioDevice()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_PnPEntity WHERE PNPClass = 'MEDIA' OR PNPClass = 'Bluetooth'"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        string name = obj["Name"]?.ToString()?.Trim();
                        if (!string.IsNullOrEmpty(name) && (name.Contains("Bose") || name.Contains("QC45") || name.Contains("Hands-Free") || name.Contains("Headphones")))
                        {
                            return name;
                        }
                    }
                }
            }
            catch { }
            return "N/A";
        }

        private void DetectMonitors()
        {
            try
            {
                int index = 1;
                using (var searcher = new ManagementObjectSearcher("SELECT Name, Caption FROM Win32_PnPEntity WHERE PNPClass = 'Monitor'"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        string name = (obj["Name"] ?? obj["Caption"])?.ToString()?.Trim();
                        if (!string.IsNullOrEmpty(name) && !name.Contains("Generic PnP Monitor"))
                        {
                            AddSensor($"Monitor {index} Model", name);
                            index++;
                        }
                    }
                }

                if (index == 1)
                {
                    using (var searcher = new ManagementObjectSearcher("SELECT UserFriendlyName FROM Win32_DesktopMonitor"))
                    {
                        foreach (var obj in searcher.Get())
                        {
                            string name = obj["UserFriendlyName"]?.ToString()?.Trim();
                            if (!string.IsNullOrEmpty(name))
                            {
                                AddSensor($"Monitor {index} Model", name);
                                index++;
                            }
                        }
                    }
                }
            }
            catch { }
        }
    }
}
