using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Threading;
using Newtonsoft.Json;
using System.Linq;

namespace ConsoleAppTest
{
    public class Program
    {
        static string repositoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDoc‌​uments), "ScanJsonApp");
        static string folderListPath = Path.Combine(repositoryPath, "FolderList.dat");
        static string configFilePath = Path.Combine(repositoryPath, "Config.dat");                                                      
        static string allReportsFolderPath = Path.Combine(repositoryPath, "All Reports");                                                   // Sets the paths for all       
        static string allReportsFolderListPath = Path.Combine(allReportsFolderPath, "AllReportsFolderList.dat");                            // the necessary folders and files
        static string lastReportFolderPath = Path.Combine(repositoryPath, "Last Reports");
        static string lastReportFolderListPath = Path.Combine(lastReportFolderPath, "LastReportFolderList.dat");
        static int scanInterval = 3000;
        static int maxJobs = 5;
        static JsonSerializerSettings jsonSettings = new();       

        public static void Main(string[] args)
        {                    
            string[] folderPaths = null;
            List<string> processReport = null;
            List<List<string>> filePaths;
            bool continousProcess = true;            

            while (continousProcess)
            {
                ReadConfig();

                try
                {
                    folderPaths = File.ReadAllLines(folderListPath);                              // Reads the list of folders to scan                                  
                }
                catch (FileNotFoundException ex) { Console.WriteLine("File with folder list not found! ({0})", ex.Message); }
                catch (IOException ex) { Console.WriteLine("Error: " + ex.Message); }
                catch (UnauthorizedAccessException ex) { Console.WriteLine("Error: " + ex.Message); }

                filePaths = new();

                if (folderPaths != null)
                {
                    foreach (string path in folderPaths)
                        filePaths.Add(ScanFolder(path));                                // Scans the folders for .json files

                    GenerateLastProcessFolderList(folderPaths);                         // Generates list of folders that were last scanned/processed
                }                    

                int counter = 0;

                if (filePaths != null)
                    foreach (List<string> files in filePaths)
                    {
                        if (files != null && files.Count > 0)
                        {
                            Console.WriteLine("\nProcessing folder: {0}\n", folderPaths[counter]);

                            processReport = ProcessFiles(files, maxJobs);                   // Processes .json files and produces a report on number of components and errors
                            processReport.Sort();
                        }
                        else
                            Console.WriteLine("\nNo files in {0}, skipping...\n", folderPaths[counter]);
                        
                        GenerateReport(processReport, lastReportFolderPath, counter);       // Writes reports as .txt files in Last Reports folder
                        GenerateOverallReport(processReport, folderPaths[counter]);         // Writes reports as .txt files in All Reports folder

                        processReport = null;
                        counter++;                                       
                    }              

                Console.WriteLine("\nFinished.");
                Console.WriteLine("\nScanning again in {0} seconds with maximum {1} concurrent processing jobs.", scanInterval/1000, maxJobs);

                folderPaths = null;
                filePaths = null;               

                Thread.Sleep(scanInterval);
            }
                
            Console.ReadLine();
        }

        public static List<string> ScanFolder(string folderPath)                // Scans the folder for .json files and returns a list of file paths
        {
            List<string> filePaths = null;

            try
            {
                Console.WriteLine("\nScanning for .json files in {0} folder...", folderPath);
                filePaths = new List<string>(Directory.EnumerateFiles(folderPath, "*.json"));           
                
                if (filePaths.Count == 1)
                    Console.WriteLine("\n - Found 1 file.");
                else
                    Console.WriteLine("\n - Found {0} files.", filePaths.Count);
            }
            catch (DirectoryNotFoundException ex) { Console.WriteLine("Directory not found! Error: " + ex.Message); }
            catch (IOException ex) { Console.WriteLine("Error: " + ex.Message); }            
            catch (UnauthorizedAccessException ex) { Console.WriteLine("Error: " + ex.Message); }
            catch (ArgumentException ex ) { Console.WriteLine("Error: " + ex.Message); }

            return filePaths;
        }

        public static List<string> ProcessFiles(List<string> filePaths, int maxJobs)
        {
            List<string> fullReport = new();
            ConcurrentBag<string> fullReportBag = new();
            int globalCount = 0;                                        // Counts the number of processed files in filePaths list
            int errorCount = 0;
            jsonSettings.MissingMemberHandling = MissingMemberHandling.Error;

            while (globalCount != filePaths.Count)
            {
                if (filePaths.Count - globalCount < maxJobs)                // Checks if the remaining number of files to be processed
                    maxJobs = filePaths.Count - globalCount;                // is less than maxJobs; ensures that count value(index) will never go out of bounds of filePaths list
                                                                            // during Parallel For loop
                Parallel.For(globalCount, maxJobs + globalCount, count =>
                {
                    bool errorReported = false;                   
                    int componentCount = 0;
                    string fileReport;

                    try
                    {
                        string jsonString = File.ReadAllText(filePaths[count]);
                        List<JsonFile> jsonFile = JsonConvert.DeserializeObject<List<JsonFile>>(jsonString, jsonSettings);

                        if (jsonFile != null)
                        {
                            foreach (JsonFile component in jsonFile)                             
                                componentCount++;
                        }                                                
                    }
                    catch (FileNotFoundException ex)
                    {
                        fileReport = filePaths[count].ToString() + " - Error: " + ex.Message.ToString();
                        fullReportBag.Add(fileReport);
                        errorReported = true;
                        Interlocked.Increment(ref errorCount);
                    }
                    catch (FormatException ex)
                    {
                        fileReport = filePaths[count].ToString() + " - Error: " + ex.Message.ToString();
                        fullReportBag.Add(fileReport);
                        errorReported = true;
                        Interlocked.Increment(ref errorCount);
                    }
                    catch (JsonException ex)
                    {
                        fileReport = filePaths[count].ToString() + " - Error: " + ex.Message.ToString();
                        fullReportBag.Add(fileReport);
                        errorReported = true;
                        Interlocked.Increment(ref errorCount);
                    }
                    catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException ex)
                    {
                        fileReport = filePaths[count].ToString() + " - Error: " + ex.Message.ToString();
                        fullReportBag.Add(fileReport);
                        errorReported = true;
                        Interlocked.Increment(ref errorCount);
                    }

                    if (!errorReported)
                    {
                        fileReport = filePaths[count].ToString() + " - " + componentCount.ToString() + " components.";
                        fullReportBag.Add(fileReport);
                    }                                     

                    Interlocked.Increment(ref globalCount);                

                });              
            }

            Console.WriteLine(" - Processed {0} files in total, {1} with errors.\n", fullReportBag.Count, errorCount);

            return fullReport = fullReportBag.ToList();
        }

        public static void GenerateOverallReport(List<string> processReport, string folderPath)         
        {
            bool folderAlreadyOnList = false;
            int counter = 0;

            try
            {
                if (!File.Exists(allReportsFolderListPath))
                {
                    using StreamWriter sw = new StreamWriter(allReportsFolderListPath);
                }

                string[] folderList = File.ReadAllLines(allReportsFolderListPath);

                foreach (string path in folderList)                         
                {
                    if (path == folderPath)                                         // Checks if folder is already on the list of previously processed folders
                    {
                        folderAlreadyOnList = true;
                        break;
                    }
                    else
                        counter++;
                }

                if (!folderAlreadyOnList)                                           // If not on list, adds it to the list
                {
                    using (StreamWriter addToFolderList = File.AppendText(allReportsFolderListPath))
                    {
                        addToFolderList.Write(folderPath + "\n");
                    }                
                }

                GenerateReport(processReport, allReportsFolderPath, counter);       // Creates or updates the report for the processed folder in All Reports folder
            }
            catch (IOException ex) { Console.WriteLine("Error: " + ex.Message); }
            catch (UnauthorizedAccessException ex) { Console.WriteLine("Error: " + ex.Message); }       
        }

        public static void GenerateReport(List<string> processReport, string filePath, int counter)         // Writes a report as .txt file in the specified folder
        {
            try
            {
                using (StreamWriter reportFile = new StreamWriter(Path.Combine(filePath, (counter.ToString() + ".txt"))))
                {
                    if(processReport != null)
                        foreach (string report in processReport)
                            reportFile.WriteLine(report);

                    if (processReport == null || processReport.Count == 0)
                        reportFile.WriteLine("No .json files found.");
                }
            }
            catch (IOException ex) { Console.WriteLine("Error: " + ex.Message); }
            catch (UnauthorizedAccessException ex) { Console.WriteLine("Error: " + ex.Message); }
            catch (ArgumentNullException ex) { Console.WriteLine("Error: " + ex.Message); }
        }

        public static void GenerateLastProcessFolderList(string[] folderPaths)                      // Generates LastReportFolderList.dat with folder paths from the last scan
        {
            try
            {
                using (StreamWriter lastProcessFolderList = new StreamWriter(lastReportFolderListPath))
                {
                    foreach (string path in folderPaths)
                        lastProcessFolderList.WriteLine(path);
                }
            }
            catch (IOException ex) { Console.WriteLine("Error: " + ex.Message); }
            catch (UnauthorizedAccessException ex) { Console.WriteLine("Error: " + ex.Message); }
            catch (ArgumentNullException ex) { Console.WriteLine("Error: " + ex.Message); }
        }

        public static void ReadConfig()
        {
            try
            {
                string[] readConfig = File.ReadAllLines(configFilePath);            // Reads Config.dat
                scanInterval = int.Parse(readConfig[1]) * 1000;                     // and assigns values
                maxJobs = int.Parse(readConfig[2]);
            }
            catch (FileNotFoundException ex) {Console.WriteLine("Config.dat file not found, using default configuration. Error: ({0})", ex.Message); }
            catch (IOException ex) { Console.WriteLine("Error: " + ex.Message); }
            catch (UnauthorizedAccessException ex) { Console.WriteLine("Error: " + ex.Message); }
        }
    }
}
