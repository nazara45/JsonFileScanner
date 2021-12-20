using System;
using System.IO;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using Newtonsoft.Json.Serialization;
using Newtonsoft.Json;

namespace ConsoleAppTest
{
    class Program
    {
        static string repositoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDoc‌​uments), "ScanJsonApp");
        static string folderListPath = Path.Combine(repositoryPath, "FolderList.dat");
        static string configFilePath = Path.Combine(repositoryPath, "Config.dat");
        static string allReportsFolderPath = Path.Combine(repositoryPath, "All Reports");
        static string allReportsFolderListPath = Path.Combine(allReportsFolderPath, "AllReportsFolderList.dat");
        static string lastReportFolderPath = Path.Combine(repositoryPath, "Last Reports");
        static string lastReportFolderListPath = Path.Combine(lastReportFolderPath, "LastReportFolderList.dat");
        static int scanInterval = 3000;
        static int maxJobs = 5;

        static void Main(string[] args)
        {                    
            string[] folderPaths = null;
            List<string> processReport;
            List<List<string>> filePaths;
            bool continousProcess = true;            

            while (continousProcess)
            {
                ReadConfig();               

                try
                {
                    folderPaths = File.ReadAllLines(folderListPath);
                }
                catch (FileNotFoundException ex)
                {
                    Console.WriteLine("File with folder list not found! ({0})", ex.Message);
                }

                filePaths = new();

                Console.WriteLine("------------------------------------------");
                Console.WriteLine("SCANNING");
                Console.WriteLine("------------------------------------------");

                if (folderPaths != null)
                    foreach (string path in folderPaths)
                    {
                        filePaths.Add(ScanFolder(path));
                    }

                int counter = 0;

                Console.WriteLine("------------------------------------------");
                Console.WriteLine("PROCESSING");
                Console.WriteLine("------------------------------------------");

                if (filePaths != null)
                    foreach (List<string> files in filePaths)
                    {
                        Console.WriteLine("Processing folder: {0}\n", folderPaths[counter]);

                        processReport = ProcessFiles(files, maxJobs);
                        processReport.Sort();
                        GenerateReport(processReport, lastReportFolderPath, counter);
                        GenerateOverallReport(processReport, folderPaths[counter]);

                        counter++;                                       
                    }

                if(folderPaths != null)
                    GenerateLastProcessFolderList(folderPaths);

                Console.WriteLine("\nFinished.");
                Console.WriteLine("\nScanning in {0} seconds...", scanInterval/1000);

                folderPaths = null;
                filePaths = null;

                Thread.Sleep(scanInterval);
            }
                
            Console.ReadLine();
        }

        public static List<string> ScanFolder(string folderPath)
        {
            List<string> filePaths = null;

            try
            {
                Console.WriteLine("Scanning for .json files in {0} folder...", folderPath);
                filePaths = new List<string>(Directory.EnumerateFiles(folderPath, "*.json"));

                if(filePaths.Count == 1)
                    Console.WriteLine("\n - Found 1 file.");
                else
                    Console.WriteLine("\n - Found {0} files.", filePaths.Count);

                Console.WriteLine("------------------------------------------");
            }
            catch (DirectoryNotFoundException ex)
            {
                Console.WriteLine("Directory not found! Error: " + ex.Message);
            }

            return filePaths;
        }

        public static List<string> ProcessFiles(List<string> filePaths, int maxJobs)
        {
            List<string> fullReport = new();
            List<JsonFile> jsonList = new();
            int globalCount = 0;           

            while (globalCount != filePaths.Count)
            {
                if (filePaths.Count - globalCount < maxJobs)
                    maxJobs = filePaths.Count - globalCount;

                Parallel.For(globalCount, maxJobs + globalCount, count =>
                {
                    bool errorReported = false;
                    int componentCount = 0;
                    string fileReport;

                    try
                    {
                        string jsonString = System.IO.File.ReadAllText(filePaths[count]);
                        dynamic jsonDynamic = JsonConvert.DeserializeObject(jsonString);
                        JsonFile jsonFile = new();

                        foreach (var item in jsonDynamic)
                        {
                            jsonFile.Name = item.name;
                            jsonFile.Status = item.status;
                            //jsonList.Add(jsonFile);
                            componentCount++;
                        }                         
                    }
                    catch (FileNotFoundException ex)
                    {
                        fileReport = filePaths[count].ToString() + " - Error: " + ex.Message.ToString();
                        fullReport.Add(fileReport);
                        errorReported = true;
                    }
                    catch (FormatException ex)
                    {
                        fileReport = filePaths[count].ToString() + " - Error: " + ex.Message.ToString();
                        fullReport.Add(fileReport);
                        errorReported = true;
                    }
                    catch (JsonException ex)
                    {
                        fileReport = filePaths[count].ToString() + " - Error: " + ex.Message.ToString();
                        fullReport.Add(fileReport);
                        errorReported = true;
                    }

                    if(!errorReported)
                    {
                        fileReport = filePaths[count].ToString() + " - " + componentCount.ToString() + " components.";
                        fullReport.Add(fileReport);
                    }                                     

                    Interlocked.Increment(ref globalCount);                

                });              

            } 
            
            return fullReport;

        }

        public static void GenerateOverallReport(List<string> processReport, string folderPath)
        {
            bool folderAlreadyOnList = false;
            int counter = 0;

            try
            {
                if (!Directory.Exists(allReportsFolderPath))
                    Directory.CreateDirectory(allReportsFolderPath);

                if (!File.Exists(allReportsFolderListPath))
                {
                    using StreamWriter sw = new StreamWriter(allReportsFolderListPath);
                }


                string[] folderListReader = File.ReadAllLines(allReportsFolderListPath);


                foreach (string path in folderListReader)
                {
                    if (path == folderPath)
                    {
                        folderAlreadyOnList = true;
                        break;
                    }
                    else
                        counter++;
                }

                if (!folderAlreadyOnList)
                {
                    using (StreamWriter addToFolderList = File.AppendText(allReportsFolderListPath))
                    {
                        addToFolderList.Write(folderPath + "\n");
                    }

                    GenerateReport(processReport, allReportsFolderPath, counter);
                }
            }
            catch (IOException) { }
                       
        }

        public static void GenerateReport(List<string> processReport, string filePath, int counter)
        {
                using (StreamWriter reportFile = new StreamWriter(Path.Combine(filePath, (counter.ToString() + ".txt"))))
                {
                    foreach (string report in processReport)
                    {
                        Console.WriteLine(" - " + report);
                        reportFile.WriteLine(report);
                    }

                    if(processReport.Count == 0) 
                        reportFile.WriteLine("No .json files found.");
                }

                Console.WriteLine("------------------------------------------");
        }

        public static void GenerateLastProcessFolderList(string[] folderPaths)
        {
            try
            {
                if (!Directory.Exists(lastReportFolderPath))
                    Directory.CreateDirectory(lastReportFolderPath);

                using (StreamWriter lastProcessFolderList = new StreamWriter(lastReportFolderListPath))
                {
                    foreach (string path in folderPaths)
                        lastProcessFolderList.WriteLine(path);
                }
            }
            catch (Exception) { }
        }

        public static void ReadConfig()
        {
            try
            {
                string[] readConfig = File.ReadAllLines(configFilePath);
                scanInterval = int.Parse(readConfig[1]) * 1000;
                maxJobs = int.Parse(readConfig[2]);
            }
            catch (FileNotFoundException ex)
            {
                Console.WriteLine("Config.dat file not found, reverting to default configuration. Error: ({0})", ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Config.dat error: " + ex.Message);
            }
        }
    }
}
