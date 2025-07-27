using System.Linq;
using System.Collections.Generic;
using LibreHardwareMonitor.Hardware;

namespace TempTrayWidget
{
    public class LoadMonitor
    {
        private readonly Computer _computer;

        public LoadMonitor()
        {
            _computer = new Computer
            {
                IsCpuEnabled = true,
                IsGpuEnabled = true
            };
            _computer.Open();
        }

        /// <summary>
        /// Returns (cpuLoad%, gpuLoad%)
        /// </summary>
        public (float cpuLoad, float gpuLoad) ReadLoads()
        {
            float cpuLoad = 0, gpuLoad = 0;
            var cpuSamples = new List<float>();
            var gpuSamples = new List<float>();

            foreach (var hw in _computer.Hardware)
            {
                hw.Update();
                if (hw.Sensors == null) continue;

                if (hw.HardwareType == HardwareType.Cpu)
                {
                    cpuSamples.AddRange(
                        hw.Sensors
                          .Where(s => s.SensorType == SensorType.Load)
                          .Select(s => s.Value ?? 0)
                    );
                }
                else if (hw.HardwareType == HardwareType.GpuAmd ||
                         hw.HardwareType == HardwareType.GpuNvidia)
                {
                    gpuSamples.AddRange(
                        hw.Sensors
                          .Where(s => s.SensorType == SensorType.Load)
                          .Select(s => s.Value ?? 0)
                    );
                }
            }

            if (cpuSamples.Any())
                cpuLoad = cpuSamples.Average();
            if (gpuSamples.Any())
                gpuLoad = gpuSamples.Average();

            return (cpuLoad, gpuLoad);
        }
    }
}
