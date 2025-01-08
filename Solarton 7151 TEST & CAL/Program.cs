using Microsoft.VisualBasic;
using System;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.IO.Ports;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Timers;
//new commmit change 
//Objectives
//Add verification of results to CAL --DONE
//format measurements -- DONE
//enter measurements into files -- DONE
//Configurability Choose COM port --DONE
//Fix COM port check -- DONE
//Data logging with standard CSV syntax -- DONE
// Clean up console --DONE
//Better error handling -- DONE
//ensure portability
//implement counts converter -- RELEASE
//implement check at end of calibration 
//fix COM port checker -- DONE
//fix ranging in calibration

namespace Solarton_7151_TEST___CAL
{
    internal class Program
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint SetThreadExecutionState(uint esFlags);

        private const uint ES_CONTINUOUS = 0x80000000;                                
        private const uint ES_SYSTEM_REQUIRED = 0x00000001;
        private const uint ES_DISPLAY_REQUIRED = 0x00000002;

        static void Main(string[] args)
        {
            SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED); // So the system does not go to sleep
            Console.WriteLine("Welcome to solartron 7151 for AD488 by Pascal Newton-Edgar"); //title
            Thread.Sleep(1000);
            bool cont = true;
            while (cont)// Main menu loop
            {

                Console.Clear();
                string[] options = { "Calibration", "Measure", "Exit program" }; 

                string ans = options[ShowMenu("Main Menu", options)];
                if (ans == "Calibration")
                {
                    Console.Clear();
                    Calibration_Routine(); // call the calibration routine
                }
                else if (ans == "Measure") 
                {
                    Console.Clear();
                    Measurment_Routine(); // call the measurement routine
                }
                else if (ans == "Exit program")
                {
                    SetThreadExecutionState(ES_CONTINUOUS); // Restore sleeping
                    Environment.Exit(0); // Exit program
                }
            }



        }



        static void Measurment_Routine()
        {
            try
            {
                string[] ports = SerialPort.GetPortNames(); //get current ports
                string COMport = ports[ShowMenu("Pick the AD488 COM port", ports)]; //put them in a menu

                using (SerialPort serialPort = new SerialPort(COMport, 115200))//initiate serial port
                {

                    serialPort.Open();
                    serialPort.ReadTimeout = 2000; // set timeout to 2 seconds                 
                                                
                    string response = "";

                    serialPort.WriteLine("++auto 0"); // set controller to not read automatically to stop tracking measurements
                    serialPort.DiscardInBuffer(); //discard buffers 
                    serialPort.DiscardOutBuffer();
                    serialPort.WriteLine("++ver");// send ver command to check if it is the ar488
                    Thread.Sleep(1000);
                    string resp = serialPort.ReadLine(); //read the response
                    Console.Clear();
                    if (resp.Contains("AR488 GPIB controller")) //if it is the ar488  
                    {
                        Console.WriteLine(resp);  //write the response
                        Console.WriteLine("Valid COM PORT");
                    }
                    else { Console.WriteLine("Couldn't verify COM port. This may be the wrong one."); Console.WriteLine(resp); }
                    serialPort.WriteLine("++auto 1"); // set the controller to read automatically


                    bool addrset = false;
                    while (addrset == false)
                    {
                        Console.Write("Enter GPIB Address:");
                        string address = Console.ReadLine();
                        serialPort.WriteLine($"++addr {address}"); // send address to controller
                        if (int.Parse(address) > 0 && int.Parse(address) < 31) // check if address is valid
                        {

                            addrset = true;
                            Console.WriteLine("Address set Correctly");
                        }
                        else { Console.WriteLine("address not set please try again"); }
                    }

                    Console.Clear();
                    string[] MODES = { "VDC", "VAC", "KOHM", "IDC", "IAC" };
                    string mode = MODES[ShowMenu("Select Mode", MODES)]; // show menu to pick mode
                    serialPort.WriteLine($"MODE {mode}"); //set mode

                    serialPort.WriteLine("RANGE AUTO"); //set range to auto
                    Console.WriteLine("RANGE set to AUTO");
                    Console.Clear();
                    bool cont = true;

                    while (cont)//measurement loop
                    {
                        Console.Clear();                        //string[] yesno = { "Yes", "No" };

                        //string ans1 = yesno[ShowMenu("Do you want to apply an offset?", yesno)];
                        //if (ans1 == "y")
                        //{
                        //    Console.WriteLine("What do you want the offset to be?");
                        //    string offset = Console.ReadLine();
                        //    serialPort.WriteLine($"SELECT OFFSET C = {offset}");
                        //}

                        string[] digoptions = { "6.5 digit", "5.5 digit Filter ON", "5.5 digit", "4.5 digit", "3.5 digit" };

                        string select = digoptions[ShowMenu("Select number of digits:", digoptions)]; // Display digit menu
                        int waittime = 0;
                        if (select == "6.5 digit")
                        {
                            serialPort.WriteLine("NINES 6");//set number of digits
                            waittime = 2000;//select correct measurement time
                        }
                        else if (select == "5.5 digit Filter ON")
                        {
                            serialPort.WriteLine("NINES 5 FILTER ON");
                            waittime = 1600;
                        }
                        else if (select == "5.5 digit")
                        {
                            serialPort.WriteLine("NINES 5");
                            waittime = 400;
                        }
                        else if (select == "4.5 digit")
                        {
                            serialPort.WriteLine("NINES 4");
                            waittime = 50;
                        }
                        else if (select == "3.5 digit")
                        {
                            waittime = 7;
                            serialPort.WriteLine("NINES 3");
                        }
                       
                            bool exitmeas = false;
                            ConsoleKey key = ConsoleKey.O; //create key
                            Console.WriteLine("Please wait...");
                            Console.Clear();
                            float average = 0; // measurement average
                            float MIN = 0;//measurement Minimum
                            float MAX = 0;//measurement max
                            int count = 0; // counter for statistics
                            float runningTotal = 0;// runningtotal for statistics
                            bool logging = false; // indicates if we are logging
                            bool prechange = false;// the value for before logging is switched indicates if logging has just beegun or is ongoing
                            int saved_waittime = waittime;
                            int length = 0; // length od logging
                            float interval = 0;//logging interval
                            List<float> datapionts = new List<float>(); // our list of datapoints/measurements
                            List<TimeSpan> times = new List<TimeSpan>();// the time of each measurement
                            TimeSpan timeSpan = new TimeSpan();
                        bool islong = false; //decides whether or not to let the user escape the logging 
                            int idealwaittime = 0; // 
                        while (key != ConsoleKey.Escape) // loop while the user does not want to leave
                        {
                            if (logging == true && prechange == true) // if logging is ongoing (not just started) use this while loop to time the intervals based on the system stopwatch
                            {
                                bool continuecheck = true; 
                                //Task keyCheckTask = Task.Run(() => CheckForKeyPress());
                                while (continuecheck == true)
                                {
                                    long elapsedMilliseconds = (long)stopwatch1.Elapsed.TotalMilliseconds; //get the elapsed time
                                    if (elapsedMilliseconds >= idealwaittime) //compare the elapsed time to the next interval time
                                    {
                                        timeSpan = stopwatch1.Elapsed;
                                        idealwaittime += waittime; // add another interval to the overall 
                                        if (((float)stopwatch1.Elapsed.TotalSeconds) >= (float)length)//if the logging has reached the time limit
                                        {
                                            logging = false;

                                        }
                                       
                                        continuecheck = false;
                                    }
                                    if(islong == true) // if its longer than 10 seconds let them stop logging during an interval (this is to ensure precision at faster measuremnt times 
                                    {

                                        if (Console.KeyAvailable)
                                        {
                                            ConsoleKey stopkey = Console.ReadKey(true).Key;//get a new key
                                            if(stopkey == ConsoleKey.L)
                                            {
                                                logging = false;  //stop logging
                                                    continuecheck = false;
                                            }
                                        }
                                    }
                                    //if (isLKeyPressed == true)
                                    //{
                                    //    logging = false;
                                    //    continuecheck = false;
                                    //    isLKeyPressed = false;
                                    //}

                                    Thread.Sleep(1);
                                }
                            }
                            else
                            {
                                Thread.Sleep(waittime); // if we are not logging just use the waittime 

                            }
                        
                            if (Console.KeyAvailable)
                            {
                                key = Console.ReadKey(true).Key; // read the key
                            }

                          //  timeSpan = stopwatch1.Elapsed;
                                
                                if (key == ConsoleKey.L) //toggle logging when l is selected
                                {
                                    prechange = logging;
                                    logging = !logging;

                                }
                                else if(key == ConsoleKey.Escape)
                                {
                                    break;
                                }
                                if (logging == true && prechange == false) // if logging is started
                                {
                                    Console.Clear();
                                    Console.WriteLine("How long would you like to log data for? (DD:HH:MM:SS)");
                                    string length_input = Console.ReadLine(); //get the length

                                    length = ConvertToSeconds(length_input);
                                    Console.Clear();
                                bool valid_interval = false;
                                while (valid_interval == false) //loop to validate interval
                                {
                                    Console.Clear();
                                    Console.WriteLine($"What do you want the interval to be in seconds? (Min = {((float)waittime / 1000)*2})");
                                    interval = float.Parse(Console.ReadLine());
                                    if(interval >= (float)waittime/1000) // interval needs to be bigger than measurement time
                                    {
                                        valid_interval = true;
                                    }
                                    else
                                    {
                                        Console.WriteLine("invalid interval press enter to try again");
                                        Console.ReadLine();
                                    }
                                }
                                if(interval > 10)
                                {
                                    islong = true;
                                }
                                else { islong = false; }
                                    waittime = (int)(interval * 1000); //set the waittime
                                    prechange = true;
                                    cts = new CancellationTokenSource();//make token for timer
                                    Task.Run(() => StartTimer(cts.Token));//start timer display
                                
                                    Console.Clear();
                                    Console.WriteLine("Please Wait..");
                                
                                    stopwatch1.Restart();// start the stopwatch
                                }


                                if ((logging == false && prechange == true) || (timerElapsed == length && length != 0))//when logging is ended
                                {
                                    stopwatch1.Stop();//stop the timing stopwatch
                                 
                                    Console.WriteLine("end logging");
                                    timerElapsed = 0; //reset all the values
                                    prechange = false;
                                    logging = false;
                                    cts.Cancel();//stop display timer
                                    cts.Dispose();
                                    waittime = saved_waittime;
                              
                                    Console.Clear() ;
                                    string[] yesno = { "No", "Yes" };
                                    int save = ShowMenu("Would you like to save this data?",yesno); //show saving menu
                                if (save == 1)//if they want to save
                                {
                                    bool invalid_name = true;
                                    while (invalid_name)//loop for the name 
                                    {
                                        Console.WriteLine("Enter Name of File (will be saved as CSV)");
                                        string filepath = Console.ReadLine();
                                        filepath = filepath + ".csv";//add csv to the filename
                                     
                                        try
                                        {
                                            using (StreamWriter writer = new StreamWriter(filepath, append: true))//start a streamwriter
                                            {
                                                string[] time_options = { "MilliSeconds", "Seconds", "Minutes", "Hours", "Days" };
                                                int time_choice = ShowMenu("Which time format would you like in the file?", time_options);//show the menu with the different times
                                                string header_choice = time_options[time_choice];
                                                times.RemoveAt(0); // remove the first time and datapiont
                                                datapionts.RemoveAt(0);
                                                invalid_name = false;
                                                int exist = ShowMenu("Is this an existing file you would like to add to? (would you like to add headers)", yesno); // display menu asking if its a new file
                                                if (exist == 1)
                                                {

                                                }
                                                else
                                                {
                                                    writer.WriteLine($"Time{header_choice},{mode}");//if you arent adding to a file add the headers
                                                }


                                                int counter = 0;
                                                if (time_choice == 0) // change the time format put into the document
                                                {
                                                    foreach (float f in datapionts)//itterate through all the measurements
                                                    {
                                                        writer.WriteLine($"{times[counter].TotalMilliseconds},{f}");
                                                        counter++;
                                                    }
                                                }
                                                else if (time_choice == 1)
                                                {
                                                    foreach (float f in datapionts)
                                                    {
                                                        writer.WriteLine($"{times[counter].TotalSeconds},{f}");
                                                        counter++;
                                                    }
                                                }
                                                else if (time_choice == 2)
                                                {
                                                    foreach (float f in datapionts)
                                                    {
                                                        writer.WriteLine($"{times[counter].TotalMinutes},{f}");
                                                        counter++;
                                                    }
                                                }
                                                else if (time_choice == 3)
                                                {
                                                    foreach (float f in datapionts)
                                                    {
                                                        writer.WriteLine($"{times[counter].TotalHours},{f}");
                                                        counter++;
                                                    }
                                                }
                                                else if (time_choice == 4)
                                                {
                                                    foreach (float f in datapionts)
                                                    {
                                                        writer.WriteLine($"{times[counter].TotalDays},{f}");
                                                        counter++;
                                                    }
                                                }
                                            }
                                        }
                                        catch (Exception e)
                                        {

                                        }


                                    }
                                }
                                idealwaittime = 0; // clear everything
                                    times.Clear();
                                    datapionts.Clear();
                                    Console.Clear();

                                }


                                serialPort.DiscardInBuffer();//clear the serial port
                                count++; 
                                serialPort.WriteLine("++read"); //read the measuremnt
                                string measurement = ""; 
                                float out_number = 0;
                                try
                                {
                                    measurement = serialPort.ReadLine();//read from serialport
                                    (measurement, out_number) = FormatData(measurement); //use the format data routine to remove unnecesary data
                                    if (out_number == 1000000) // if it is invalid it will be 1000000 so we dont use iit for statistics
                                    { }
                                    else
                                    {
                                        if (out_number > MAX) { MAX = out_number; } //if its bigger than max make it new max
                                        if (out_number < MIN) { MIN = out_number; }// if irs snaller than min make it new min
                                        if (count == 1) { runningTotal = out_number; MIN = out_number; }//if its the start set initial value
                                        else
                                        {
                                            runningTotal += out_number; // if its not at the start add to the running total
                                            average = runningTotal / count;
                                        }
                                    }
                                }
                                catch (TimeoutException) { measurement = "........."; }
                               
                            Console.SetCursorPosition(0, 0); // output all the data
                                Console.WriteLine($"Measurement Mode\n--------------------------------------\n\n      {measurement} {mode}\n\n--------------------------------------\nAverage:{average} Min:{MIN} Max:{MAX}\n--------------------------------------\n|Keys:|Reset(R)|Start/Stop logging (L)|Exit (Esc)");
                            Console.SetCursorPosition(0, 11);
                            Console.WriteLine("                                    ");
                                
                                if (key == ConsoleKey.R)//if reset is pressed
                                {
                                    count = 0; //reset all values to new number
                                    average = out_number;
                                    MIN = out_number;
                                    MAX = out_number;
                                }




                                if (logging == true && prechange == true) // if it is logging

                                {
                                    datapionts.Add(out_number);// //add the data
                                
                                    times.Add(timeSpan); //add the time
                                }


                                key = ConsoleKey.A;//set the key to something random
                            }
                        
                    

                        Console.WriteLine("Continue? y/n");
                        string ans = Console.ReadLine();
                        if (ans == "y") { }
                        else { cont = false; }



 
                    }

                    serialPort.WriteLine("LOCK OFF");


                }

            }



            catch (TimeoutException)
            {
                Console.WriteLine("Timeout: No response received.");
                Console.ReadLine();
            }
            catch (UnauthorizedAccessException)
            {
                Console.WriteLine("Access denied: Make sure no other application is using COM8.");
                Console.ReadLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred: " + ex.Message);
                Console.ReadLine();
            }
        }
        static CancellationTokenSource cts = new CancellationTokenSource();
       
        static void Calibration_Routine()
        {
            try
            {
                string[] ports = SerialPort.GetPortNames();//get all the ports that are available
                string COMport = ports[ShowMenu("Pick the AD488 COM port", ports)];//list out the ports in a menu
                using (SerialPort serialPort = new SerialPort(COMport, 115200)) //start out the serial
                {
                    serialPort.Open(); //open the port
                    serialPort.ReadTimeout = 2000;
                    serialPort.WriteLine("++auto 0"); // set controller not to read automatically
                    serialPort.DiscardOutBuffer();
                    serialPort.WriteLine("++ver");
                    Thread.Sleep(1000);
                    string resp = serialPort.ReadLine();
                    Console.Clear();
                    if (resp.Contains("AR488 GPIB controller"))   
                    {
                        Console.WriteLine(resp);
                        Console.WriteLine("Valid COM PORT");
                    }
                    else { Console.WriteLine("Couldn't verify COM port. This may be the wrong one."); }

                    string response = "";
                    serialPort.WriteLine("++auto 1");//set ti read automatically
                    bool addrset = false;
                    while (addrset == false)
                    {
                        Console.Write("Enter GPIB Address:");
                        string address = Console.ReadLine();
                        serialPort.WriteLine($"++addr {address}"); // send address to controller
                        if (int.Parse(address) > 0 && int.Parse(address) < 31)
                        {
                            addrset = true;
                            Console.WriteLine("Address set Correctly");//repeat of ealier code
                        }
                        else { Console.WriteLine("address not set please try again"); }
                    }
                    Console.Clear();
                    bool cont = true;
                    while (cont)
                    {
                        serialPort.WriteLine("TRACK OFF");// turn tracking off to prevent bad values
                        Console.Clear();
                        string[] MODES = { "VDC", "VAC", "KOHM", "IDC", "IAC" };
                        string mode = MODES[ShowMenu("Select Mode", MODES)];
                        serialPort.WriteLine($"MODE {mode}");

                        Console.Clear();
                        Console.WriteLine("Connect Measurement source to check range. Press enter to continue");
                        Console.ReadLine();
                        serialPort.WriteLine("RANGE AUTO"); //auto range the device on the measurement source
                        serialPort.DiscardOutBuffer();
                        serialPort.DiscardInBuffer();
                        serialPort.WriteLine("RANGE ?");//query the range
                        serialPort.WriteLine("++read");
                        Console.WriteLine(serialPort.ReadLine());//write the range
                        Console.ReadLine();
                        Console.Clear();

                        string[] yesno = { "No", "Yes" };
                        string ans = yesno[ShowMenu("Are you sure you want to enter calibration mode?", yesno)];
                        if (ans == "Yes")
                        {
                            serialPort.WriteLine("CALIBRATE ON"); // turn on calibration
                            Console.WriteLine("calibration enabled");
                        }
                        else
                        {
                            break;
                        }


                        //  Console.WriteLine("what range do you want to calibrate?(in kOhms)");
                        // string RANGE = Console.ReadLine();                                      //setting fixed ranges seems to lead to zeroing errors?
                        //  serialPort.WriteLine($"RANGE {RANGE}");                      
                        // serialPort.WriteLine("RANGE AUTO");
                        // serialPort.WriteLine("DISPLAY [CONN ZERO]");
                        Console.WriteLine("Connect Zero source. Press enter to continue");
                        Console.ReadLine();
                        serialPort.DiscardInBuffer();
                        serialPort.WriteLine("LO 0");//do the Low calibration
                        Thread.Sleep(5000);//wait until it is completed
                        serialPort.WriteLine("++read");
                        Console.WriteLine(serialPort.ReadLine());
                        Console.ReadLine();
                        //  serialPort.WriteLine($"RANGE {RANGE}");

                        //Console.Write("Do you need to apply an offset? y/n: ");
                        //ans = Console.ReadLine();
                        //if (ans == "y")
                        //{
                        //    Console.WriteLine("What do you want the offset to be?");
                        //    string offset = Console.ReadLine();
                        //    serialPort.WriteLine("COMPUTE ON");
                        //    serialPort.WriteLine($"SELECT OFFSET C = {offset}");
                        //}
                        //  serialPort.WriteLine($"RANGE {RANGE}");
                        Console.Clear();
                        Console.WriteLine("Connect measurement source. Enter the known counts out of your source to continue\n (refer to manual if your not sure)(e.g 9.9998kohm = 099998 and 100.003kohm = 100003");
                        string measuredval = Console.ReadLine();
                        serialPort.DiscardInBuffer();
                        Console.WriteLine(measuredval);
                        serialPort.WriteLine($"HI {measuredval}");//do high calibration
                        Thread.Sleep(5000);//wait till completed
                        serialPort.WriteLine("++read");
                        Console.WriteLine(serialPort.ReadLine());//display returned value
                        Console.ReadLine();
                        Console.Clear();
                        int Write_ans = ShowMenu("Would you like to WRITE this data?", yesno);
                        if (Write_ans == 0) { serialPort.WriteLine("CALIBRATE OFF"); break; } //if they say no turn off calibration and exit
                        else
                        {
                            serialPort.WriteLine("WRITE");//write cal data
                        }
                        Thread.Sleep(1000);
                        serialPort.WriteLine("++read");
                        string CAL_response = serialPort.ReadLine();
                        Console.WriteLine("Calibration Response: " + CAL_response); //show cal response
                        serialPort.WriteLine("CALIBRATE OFF");
                        Console.WriteLine("Would you like to calibrate another range y/n ?");
                        ans = Console.ReadLine();
                        if (ans == "y")
                        {

                        }
                        else { cont = false; serialPort.WriteLine("TRACK ON"); }
                    }


                }

            }
            catch (TimeoutException)
            {
                Console.WriteLine("Timeout: No response received.");
                Console.ReadLine();

            }
            catch (UnauthorizedAccessException)
            {
                Console.WriteLine("Access denied: Make sure no other application is using COM8.");
                Console.ReadLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred: " + ex.Message);
                Console.ReadLine();
            }
        }


        static int ShowMenu(string title, string[] options)//routine to show menus and return result
        {
            int selectedIndex = 0;
            ConsoleKey key;

            do
            {
                Console.Clear();
                Console.WriteLine(title);
                Console.WriteLine(new string('-', title.Length));

                // Render the menu
                for (int i = 0; i < options.Length; i++)
                {
                    if (i == selectedIndex)
                    {
                        Console.BackgroundColor = ConsoleColor.Gray; // Highlight background
                        Console.ForegroundColor = ConsoleColor.Black; // Highlight text
                        Console.WriteLine(options[i]);
                        Console.ResetColor(); // Reset colors
                    }
                    else
                    {
                        Console.WriteLine(options[i]);
                    }
                }

                // Read user input
                key = Console.ReadKey(true).Key;

                switch (key)
                {
                    case ConsoleKey.UpArrow:
                        selectedIndex = (selectedIndex == 0) ? options.Length - 1 : selectedIndex - 1;
                        break;

                    case ConsoleKey.DownArrow:
                        selectedIndex = (selectedIndex == options.Length - 1) ? 0 : selectedIndex + 1;
                        break;

                    case ConsoleKey.Enter:
                        return selectedIndex; // Return the selected index
                }

            } while (key != ConsoleKey.Escape); // Optional: allow user to exit with Escape

            return -1; // Return -1 if Escape is pressed
        }

        static (string, float) FormatData(string data)
        {

            if (data.Contains('!')) // if it is not connected
            {
                return ("NA", 1000000);//return NA and a value that could never be returned normally
            }
            float num_return;
            data = data.Trim();//trim all whitespace
            if (data[0] == '+')
            {
                data = data.Substring(1);//remove + sign
            }
            int space_position = data.IndexOf(' ');
            data = data.Substring(0, space_position);

            num_return = float.Parse(data);


            return (data, num_return);
        }

        static int ConvertToSeconds(string time)
        {
            // Split the time string into parts
            string[] parts = time.Split(':');

            if (parts.Length != 4)
            {
                throw new ArgumentException("Input must be in the format dd:hh:mm:ss");
            }

            // Parse each part
            int days = int.Parse(parts[0]);
            int hours = int.Parse(parts[1]);
            int minutes = int.Parse(parts[2]);
            int seconds = int.Parse(parts[3]);

            // Calculate total seconds
            int totalSeconds = (days * 24 * 60 * 60) + // Days to seconds
                                (hours * 60 * 60) +    // Hours to seconds
                                (minutes * 60) +       // Minutes to seconds
                                seconds;               // Already in seconds

            return totalSeconds;
        }
        static int timerElapsed = 0;
        static void StartTimer(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {

                Console.SetCursorPosition(0, 10);
                int hours = timerElapsed / 3600;
                int minutes = (timerElapsed % 3600) / 60;
                int seconds = timerElapsed % 60;
                Console.WriteLine($"Logging for: {hours:D2}:{minutes:D2}:{seconds:D2}");
                // Increment the timer and wait 1 second
                timerElapsed++;
                Thread.Sleep(1000);
            }
        }
     
       public static Stopwatch stopwatch1 = new Stopwatch();// logging timing timer
       
    }

}

