using System.Linq;
using LibreHardwareMonitor.Hardware;

namespace TempTrayWidget
{
    public class TemperatureMonitor
    {
        private readonly Computer _computer;

        public TemperatureMonitor()
        {
            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true
            };
            _computer.Open();
        }

        public (float cpu, float gpu) ReadTemperatures()
        {
            float cpuTemp = 0, gpuTemp = 0;
            // average across sensors
            foreach (var hw in _computer.Hardware)
            {
                hw.Update();
                if (hw.Sensors == null) continue;
                if (hw.HardwareType == HardwareType.Cpu)
                    cpuTemp = hw.Sensors
                                 .Where(s => s.SensorType == SensorType.Temperature)
                                 .Select(s => s.Value ?? 0)
                                 .DefaultIfEmpty()
                                 .Average();
                if (hw.HardwareType == HardwareType.GpuAmd ||
                    hw.HardwareType == HardwareType.GpuNvidia)
                    gpuTemp = hw.Sensors
                                 .Where(s => s.SensorType == SensorType.Temperature)
                                 .Select(s => s.Value ?? 0)
                                 .DefaultIfEmpty()
                                 .Average();
            }
            return (cpuTemp, gpuTemp);
        }
    }
}
