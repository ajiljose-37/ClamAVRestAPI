using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace ClamAVRestAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ScanV2Controller : ControllerBase
    {
        [HttpPost("scan")]
        public async Task<IActionResult> ScanFile(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return BadRequest(new { error = "No file provided" });
                }

                // Save the file to a temporary location
                var tempFilePath = Path.Combine(Path.GetTempPath(), file.FileName);
                using (var stream = new FileStream(tempFilePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Run ClamAV scan
                var result = RunClamAVScan(tempFilePath);

                //Run PowerShell scan
                //var result = ScanWithWindowsDefender(tempFilePath);

                // Delete the file after scanning
                System.IO.File.Delete(tempFilePath);

                if (result.Contains("OK"))
                {
                    return Ok(new { message = "File is clean." });
                }
                else
                {
                    return Ok(new { message = "File is infected.", details = result });
                }
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        private string RunClamAVScan(string filePath)
        {
            // Create a process to run the ClamAV clamscan command
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "C:\\Users\\ajil.jose\\Downloads\\clamav-1.3.1.win.x64\\clamav-1.3.1.win.x64\\clamscan", // Make sure clamscan is in your PATH
                        Arguments = filePath,
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    }
                };

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                return output;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return ex.ToString();
            }

        }

        public string ScanWithWindowsDefender(string filePath)
        {
            try
            {
                string powerShell = "C:\\Windows\\WinSxS\\wow64_microsoft-windows-powershell-exe_31bf3856ad364e35_10.0.19041.3996_none_e7e7d1c1ebfac592\\powershell.exe";
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = powerShell,
                        Arguments = $"-Command \"Start-MpScan -ScanPath '{filePath}'\"",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    }
                };

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                return output;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                return e.ToString();
            }
        }

    }
}
