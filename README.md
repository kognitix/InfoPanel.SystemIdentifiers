# InfoPanel System Identifiers

An extension for [InfoPanel](https://github.com/habibrehmansg/infopanel) that exposes detailed hardware and software system information as text sensors.

## Features & Sensors Exposed

### Hardware
* **CPU:** Model, Codename, Socket
* **Motherboard:** Model, Chipset, BIOS Version, BIOS Date
* **GPU:** Model, Board Partner, Memory Type, PCIe Config
* **Storage:** SSD Models & Interfaces (Up to 4 drives)
* **RAM:** Brand & Specs per DIMM slot (Up to 4 slots)
* **Monitors:** Model, Resolution, Refresh Rate (Up to 4 displays)
* **Network & Cooling:** Adapter Name, Network Type, Local/Public IP, AIO Liquid Cooler detection

### Software
* __NEW__ **Uptime:** System Uptime Sensor
* **OS:** Windows Version & Build Number
* **System:** Power Plan, Windows Security Status, Windows Update Status
* **Runtimes & Drivers:** DirectX Version, .NET Runtime, GPU Driver Version
* **Utilities:** InfoPanel Version, HWiNFO Version, Process Lasso Version, Active Gaming/Utility Apps

---

## Installation

1. Download `InfoPanel.SystemIdentifiers.zip` from the latest **[Releases](../../releases)** page.
2. Open **InfoPanel**.
3. Go to **Plugins** $\rightarrow$ **User Plugins**.
4. Click **Import Plugin** and select the downloaded `.zip` file.
