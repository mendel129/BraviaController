using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using ManagedUPnP;
using ManagedUPnP.Descriptions;
using Newtonsoft.Json;
using System.Net;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace WindowsFormsApplication1
{


    public partial class Form1 : Form
    {

        public Form1()
        {
            InitializeComponent();
        }

        #region Enumerations

        /// <summary>
        /// The type of an argument switch.
        /// </summary>
        enum SwitchType
        {
            /// <summary>
            /// The switch is on.
            /// </summary>
            On,

            /// <summary>
            /// The switch is off.
            /// </summary>
            Off,

            /// <summary>
            /// Not a switch argument.
            /// </summary>
            NotASwitch,

            /// <summary>
            /// The argument was invalid.
            /// </summary>
            NA
        }

        #endregion

        #region Locals

        /// <summary>
        /// State variable for whether to check for GeTExternalIPAddress action or not.
        /// </summary>
        bool mbCheckForAction = false;

        /// <summary>
        /// State variable to determine to log UPnP information.
        /// </summary>
        bool mbVerboseUPnPLog = false;

        /// <summary>
        /// State variable for length of search timeout in milliseconds.
        /// </summary>
         int miTimeout = 5000;

         bool refresh = true;
         SonyCommands dataSet = new SonyCommands();

         //sonybraviaclass should contain all usfull information like ip, mac, cookie and commands
         SonyBraviaClass test = new SonyBraviaClass();
         
        
        Service sonytv = null;

         public string pincode = "0000";
         CookieContainer allcookies = new CookieContainer();

         // Cached Socket object that will be used by each call for the lifetime of this class
         Socket _socket = null;
         // Signaling object used to notify when an asynchronous operation is completed
         static ManualResetEvent _clientDone = new ManualResetEvent(false);
         // Define a timeout in milliseconds for each asynchronous call. If a response is not received within this 
         // timeout period, the call is aborted.
         const int TIMEOUT_MILLISECONDS = 5000;
         // The maximum size of the data buffer to use with the asynchronous socket methods
         const int MAX_BUFFER_SIZE = 2048;


        #endregion

        #region Static Methods

        /// <summary>
        /// Writes the header to the console.
        /// </summary>
        void DisplayHeader()
        {
            Console.WriteLine(
                "UPnP External IP Address Resolver\n" +
                "  Web: http://managedupnp.codeplex.com/\n");
        }

        /// <summary>
        /// Writes the help to the console.
        /// </summary>
        void DisplayHelp()
        {
            Console.WriteLine(
                "ExternalIPAddress [/T:timeout][/C][/V]\n" +
                "\n" +
                "  /T:timeout    Timeout in milliseconds (-1 for indefinite).\n" +
                "  /C            Checks for action before executing.\n" +
                "  /V            Outputs verbose UPnP log.\n");
        }

        /// <summary>
        /// Performs the search of the services.
        /// </summary>
        /// <param name="timeout">The timeout in milliseconds for the search.</param>
        /// <param name="checkForAction">True to check for action before execution.</param>
        /// <param name="verboseLog">True to write verbose UPnP log information.</param>
        void Search(int timeout, bool checkForAction, bool verboseLog)
        {
            try
            {
                Form2 tempform = new Form2();
                tempform.Show();
                textBox1.Text += "search()\r\n";
                const string csGetExternalIPAddressAction = "X_SendIRCC";

                // Used in cache two services on a device return the same IP information
                HashSet<string> lhsDone = new HashSet<string>();

                // Find the services
                bool lbCompleted;
                Services lsServices = Discovery.FindServices(
                   null,
                   timeout, 0,
                   out lbCompleted,
                   AddressFamilyFlags.IPvBoth);

                foreach (Service lsService2 in lsServices)
                {
                    if (lsService2.Description().Actions.ContainsKey(csGetExternalIPAddressAction))
                    {
                        sonytv = lsService2;
                        textBox1.Text += "name:" + sonytv.Name.ToString() + "\r\n";
                        textBox1.Text += "id: " + sonytv.FriendlyServiceTypeIdentifier + "\r\n hostaddress: " + sonytv.Device.RootHostAddresses[0] + sonytv.ToString() + "\r\n";
                        test.ipadress = sonytv.Device.RootHostAddresses[0].ToString();


                        string mac = findmac().ToString();
                        test.macadres = mac;
                        textBox1.Text += "mac found: " + mac + "\r\n";
                        textBox2.Text = mac;
                        getCommands();
                    }

                }
                Service lsService = sonytv;

                tempform.Close();
            }
            catch { textBox1.Text += "failed searching \r\n"; }
        }

        /// <summary>
        /// Occurs when a Managed UPnP log entry is raised.
        /// </summary>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="a">The event arguments.</param>
        void Logging_LogLines(object sender, LogLinesEventArgs a)
        {
            string lsLineStart = "[UPnP] " + new String(' ', a.Indent * 4);
            Console.WriteLine(lsLineStart + a.Lines.Replace("\r\n", "\r\n" + lsLineStart));
        }

        /// <summary>
        /// Decodes an argument.
        /// </summary>
        /// <param name="arg">The argument string to decode.</param>
        /// <param name="name">Contains the name of the switch on return.</param>
        /// <param name="value">Contains the value of the argument on return.</param>
        /// <param name="switchType">Contains the type of switch on return.</param>
        /// <returns>True if the argument was valid false otherwise.</returns>
        bool DecodeArgument(string arg, out string name, out string value, out SwitchType switchType)
        {
            bool lbValid = false;

            name = string.Empty;
            value = string.Empty;
            switchType = SwitchType.NA;

            // Check for switch
            if (arg.StartsWith("/"))
            {
                // If the switch is valid
                if (arg.Length > 1)
                {
                    // Determine switch type
                    switch (arg[1])
                    {
                        default: switchType = SwitchType.On; break;
                        case '-': { arg = "/" + arg.Substring(2); switchType = SwitchType.Off; break; }
                        case '+': { arg = "/" + arg.Substring(2); switchType = SwitchType.On; break; }
                    }

                    // Check for value
                    int liPos = arg.IndexOf(":");
                    value = string.Empty;

                    // Get name if no value
                    if (liPos == -1)
                        name = arg.Substring(1).ToUpper();
                    else
                    {
                        // Get name and value
                        name = arg.Substring(1, liPos - 1).ToUpper();
                        value = arg.Substring(liPos + 1);
                    }

                    lbValid = true;
                }
            }
            else
            {
                // Use argument as value only
                value = arg;
                lbValid = true;
            }

            return lbValid;
        }

        /// <summary>
        /// Processes a command-line argument.
        /// </summary>
        /// <param name="argsProcessed">The switch argument names already processed.</param>
        /// <param name="arg">The argument string to process.</param>
        /// <returns>True if argument was valid, false otherwise.</returns>
        bool ProcessArg(HashSet<string> argsProcessed, string arg)
        {
            bool lbValid = false;

            string lsName, lsValue;
            SwitchType lstType;

            if (!DecodeArgument(arg, out lsName, out lsValue, out lstType))
                lbValid = false;
            else
            {
                if (lstType == SwitchType.On)
                {
                    if (argsProcessed.Contains(lsName)) lbValid = false;
                    argsProcessed.Add(lsName);

                    switch (lsName)
                    {
                        case "T":
                            lbValid = Int32.TryParse(lsValue, out miTimeout);
                            break;

                        case "C":
                            mbCheckForAction = true;
                            lbValid = true;
                            break;

                        case "V":
                            mbVerboseUPnPLog = true;
                            lbValid = true;
                            break;
                    }
                }
                else
                    lbValid = false;
            }

            return lbValid;
        }


        public void HowToMakeRequestsToHttpBasedServices()
        {
            Uri serviceUri = new Uri("http://192.168.1.9/sony/system");
            WebClient downloader = new WebClient();
            downloader.OpenReadCompleted += new OpenReadCompletedEventHandler(downloader_OpenReadCompleted);
            downloader.OpenReadAsync(serviceUri);
        }

        void downloader_OpenReadCompleted(object sender, OpenReadCompletedEventArgs e)
        {
            if (e.Error == null)
            {
                Stream responseStream = e.Result;

                Console.Write(responseStream);
            }
        }



        private string wakeup(string macAddress)
        {
            Byte[] datagram = new byte[102];

            for (int i = 0; i <= 5; i++)
            {
                datagram[i] = 0xff;
            }

            string[] macDigits = null;
            if (macAddress.Contains("-"))
            {
                macDigits = macAddress.Split('-');
            }
            else
            {
                macDigits = macAddress.Split(':');
            }

            if (macDigits.Length != 6)
            {
                throw new ArgumentException("Incorrect MAC address supplied!");
            }

            int start = 6;
            for (int i = 0; i < 16; i++)
            {
                for (int x = 0; x < 6; x++)
                {
                    datagram[start + i * 6 + x] = (byte)Convert.ToInt32(macDigits[x], 16);
                }
            }

            UdpClient client = new UdpClient();
            client.Send(datagram, datagram.Length, "255.255.255.255", 3);
            //return Send(datagram);
            return "hoi";

        }

        public string Send(byte[] payload)//string data)
        {
            string response = "Len's Wake-Up Operation Timeout";
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

            // We are re-using the _socket object that was initialized in the Connect method
            if (_socket != null)
            {
                // Create SocketAsyncEventArgs context object
                SocketAsyncEventArgs socketEventArg = new SocketAsyncEventArgs();

                // Set properties on context object
                socketEventArg.RemoteEndPoint = new IPEndPoint(IPAddress.Broadcast, 3);   //new DnsEndPoint(serverName, portNumber);

                // Inline event handler for the Completed event.
                // Note: This event handler was implemented inline in order to make this method self-contained.
                socketEventArg.Completed += new EventHandler<SocketAsyncEventArgs>(delegate(object s, SocketAsyncEventArgs e)
                {
                    response = e.SocketError.ToString();

                    // Unblock the UI thread
                    _clientDone.Set();
                });

                // Add the data to be sent into the buffer
                //byte[] payload = data.ToArray();//Encoding.UTF8.GetBytes(data);
                socketEventArg.SetBuffer(payload, 0, payload.Length);

                // Sets the state of the event to nonsignaled, causing threads to block
                _clientDone.Reset();

                // Make an asynchronous Send request over the socket
                _socket.SendToAsync(socketEventArg);

                // Block the UI thread for a maximum of TIMEOUT_MILLISECONDS seconds.
                // If no response comes back within this time then proceed
                _clientDone.WaitOne(TIMEOUT_MILLISECONDS);


            }
            else
            {
                response = "Socket is not initialized";
            }

            return response;
        }



        public string ConvertToSonyCommand(string lookingfor)
        {
            string SonyCommand = "";
            if (refresh == true)
            {
                var httpWebRequest = (HttpWebRequest)WebRequest.Create("http://192.168.1.9/sony/system");
                httpWebRequest.ContentType = "text/json";
                httpWebRequest.Method = "POST";

                using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
                {
                    string json = "{\"id\":20,\"method\":\"getRemoteControllerInfo\",\"version\":\"1.0\",\"params\":[]}";
                    streamWriter.Write(json);
                }
                var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    var responseText = streamReader.ReadToEnd();
                    dataSet = JsonConvert.DeserializeObject<SonyCommands>(responseText);
                    refresh = false;

                }
            }

            string first = dataSet.result[1].ToString();
            List<IndiSonyCommands> ListSonyCommands = JsonConvert.DeserializeObject<List<IndiSonyCommands>>(first);
            SonyCommand = ListSonyCommands.Find(x => x.name.ToLower() == lookingfor.ToLower()).value.ToString();

            return SonyCommand;
        }

        public void getCommands()
        {
            if (refresh == true)
            {
                var httpWebRequest = (HttpWebRequest)WebRequest.Create("http://192.168.1.9/sony/system");
                httpWebRequest.ContentType = "text/json";
                httpWebRequest.Method = "POST";

                using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
                {
                    string jsonold = "{ \"method\": \"send\", " +
                                      "  \"params\": [ " +
                                      "             \"IPutAGuidHere\", " +
                                      "             \"msg@MyCompany.com\", " +
                                      "             \"MyTenDigitNumberWasHere\", " +
                                      "             \"" + "message" + "\" " +
                                      "             ] " +
                                      "}";

                    string json = "{\"id\":20,\"method\":\"getRemoteControllerInfo\",\"version\":\"1.0\",\"params\":[]}";

                    streamWriter.Write(json);
                }
                var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    var responseText = streamReader.ReadToEnd();
                    //Now you have your response.
                    //or false depending on information in the response
                    //Console.Write(responseText);


                    dataSet = JsonConvert.DeserializeObject<SonyCommands>(responseText);

                    refresh = false;

                }
            }
            string first = dataSet.result[1].ToString();
            //Console.Write(first);
            List<IndiSonyCommands> bal = JsonConvert.DeserializeObject<List<IndiSonyCommands>>(first);
            //Console.Write(lookingfor + " = " + bal.Find(x => x.name == lookingfor).value);
            //IndiSonyCommands
            test.commands = bal;



            string yetanothervariable = JsonConvert.SerializeObject(test);
            System.IO.StreamWriter file = new System.IO.StreamWriter("settings.json");
            file.WriteLine(yetanothervariable);
            file.Close();
        
        }

        public string findmac()
        {
            String macadres = "";

            var httpWebRequest = (HttpWebRequest)WebRequest.Create("http://192.168.1.9/sony/system");
            httpWebRequest.ContentType = "text/json";
            httpWebRequest.Method = "POST";

            using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
            {
                //string json = "{\"id\":20,\"method\":\"getRemoteControllerInfo\",\"version\":\"1.0\",\"params\":[]}";
                string json = "{\"id\":19,\"method\":\"getSystemSupportedFunction\",\"version\":\"1.0\",\"params\":[]}\"";
                streamWriter.Write(json);
            }
            var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                var responseText = streamReader.ReadToEnd();
                dataSet = JsonConvert.DeserializeObject<SonyCommands>(responseText);

            }

            string first = dataSet.result[0].ToString();
            List<IndiSonyOption> bal = JsonConvert.DeserializeObject<List<IndiSonyOption>>(first);
            // test.commands = bal;
            macadres = bal.Find(x => x.option.ToLower() == "WOL".ToLower()).value.ToString();

            return macadres;
        }


        #region button_event_handlers
        public void PressButton(string ToSend)
        {
            try
            {
                if (sonytv != null)
                {
                    string command = ConvertToSonyCommand(ToSend);
                    sonytv.InvokeAction("X_SendIRCC", command);
                }
                else
                {
                    textBox1.Text += "not yet found \r\n";
                }
            }
            catch { textBox1.Text += "error in sending command \r\n"; }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            PressButton("Volumeup");

        }

        private void button2_Click(object sender, EventArgs e)
        {
            PressButton("VolumeDown");
        }

        private void button4_Click(object sender, EventArgs e)
        {
            PressButton("mute");
        }

        private void button5_Click(object sender, EventArgs e)
        {
            PressButton("poweroff");

        }

        private void Up_Click(object sender, EventArgs e)
        {
            PressButton("up");
        }

        private void Down_Click(object sender, EventArgs e)
        {
            PressButton("down");
        }
        private void Left_Click(object sender, EventArgs e)
        {
            PressButton("left");
        }
        private void Right_Click(object sender, EventArgs e)
        {
            PressButton("right");
        }
        private void Home_Click(object sender, EventArgs e)
        {
            PressButton("home");
        }
        private void Back_Click(object sender, EventArgs e)
        {
            PressButton("Return");
        }
        private void button9_Click(object sender, EventArgs e)
        {
            PressButton("Confirm");
        }
        private void options_Click(object sender, EventArgs e)
        {
            PressButton("Options");
        }
        #endregion
        
        private void button3_Click(object sender, EventArgs e)
        {
            string mac = textBox2.Text;
            textBox1.Text += wakeup(mac);
            try
            {
                textBox1.Text += "start find\r\n";
                Search(miTimeout, mbCheckForAction, mbVerboseUPnPLog);
                textBox1.Text += "done searching\r\n";
                //getCommands();
            }
            catch { textBox1.Text += "no sony found, try wakeing it! \r\n"; }
        }
         



        private void button6_Click(object sender, EventArgs e)
        {
            try
            {
                textBox1.Text += "start find\r\n";
                Search(miTimeout, mbCheckForAction, mbVerboseUPnPLog);
                textBox1.Text += "done searching\r\n";
                //string mac = findmac().ToString();
                //test.macadres = mac;
                //textBox1.Text += "mac found: " + mac + "\r\n";
                //textBox2.Text = mac;
            }
            catch { textBox1.Text += "failed searching"; }
        }

        private void button7_Click(object sender, EventArgs e)
        {
            string yetanothervariable = JsonConvert.SerializeObject(test);


            System.IO.StreamWriter file = new System.IO.StreamWriter("settings.json");
            file.WriteLine(yetanothervariable);
            file.Close();


            System.IO.StreamReader myFile = new System.IO.StreamReader("settings.json");
            string myString = myFile.ReadToEnd();
            myFile.Close();

            SonyBraviaClass newtest = JsonConvert.DeserializeObject<SonyBraviaClass>(myString);
        }

 

        private void button8_Click(object sender, EventArgs e)
        {
            string thetext = textBox3.Text;
            string jsontosend = "{\"id\":78,\"method\":\"setTextForm\",\"version\":\"1.0\",\"params\":[\"" + thetext + "\"]}";

            var httpWebRequest = (HttpWebRequest)WebRequest.Create("http://192.168.1.9/sony/appControl");
            httpWebRequest.ContentType = "application/json";
            httpWebRequest.Method = "POST";
            //httpWebRequest.CookieContainer = allcookies;
            httpWebRequest.CookieContainer = new CookieContainer();
            //httpWebRequest.CookieContainer.Add(new Uri("http://192.168.1.9/sony/appControl"), new Cookie("auth", "91daa100c306e4dbe75a91c1153fe3d83bd65e1b4d07b3e76b443b56bd72a6b0"));
            //httpWebRequest.CookieContainer.Add(new Uri("http://" + test.ipadress + "/sony/appControl"), new Cookie("auth", test.cookie));
            //request.CookieContainer.Add(new Uri("http://api.search.live.net"),new Cookie("id", "1234"));
            //Cookie=auth=f85152252ac0265e4e90cc97fbdbdbb92e2d5ec0313c1b5d168d8badb69f8363

            //string test = JsonConvert.SerializeObject(httpWebRequest.CookieContainer.GetCookies(new Uri("http://192.168.1.9/sony/appControl")));

            // Write the string to a file.
            //System.IO.StreamWriter file = new System.IO.StreamWriter("cookie.json");
            //file.WriteLine(test);

            //file.Close();



            // Read the file as one string.
            System.IO.StreamReader myFile =
            new System.IO.StreamReader("cookie.json");
            string myString = myFile.ReadToEnd();

            myFile.Close();

            List<IndiCookie> bal = JsonConvert.DeserializeObject<List<IndiCookie>>(myString);
            //IndiCookie bal = JsonConvert.DeserializeObject<IndiCookie>(myString);
            httpWebRequest.CookieContainer.Add(new Uri("http://192.168.1.9/sony/appControl"), new Cookie(bal[0].Name, bal[0].Value));


            //httpWebRequest.Headers["Cookie"] = "auth=f85152252ac0265e4e90cc97fbdbdbb92e2d5ec0313c1b5d168d8badb69f8363";
            using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
            {
                //string json = "{\"id\":20,\"method\":\"getRemoteControllerInfo\",\"version\":\"1.0\",\"params\":[]}";
                //string json = "{\"id\":19,\"method\":\"getSystemSupportedFunction\",\"version\":\"1.0\",\"params\":[]}\"";
                streamWriter.Write(jsontosend);
            }
            var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                var responseText = streamReader.ReadToEnd();
                //dataSet = JsonConvert.DeserializeObject<SonyCommands>(responseText);
                textBox1.Text += responseText + "\r\n";
            }
        }

     
        //keyboard control
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Up))
            {
                Up.PerformClick();
                return true;
            }
            if (keyData == (Keys.Down))
            {
                Down.PerformClick();
                return true;
            }
            if (keyData == (Keys.Left))
            {
                Left.PerformClick();
                return true;
            }
            if (keyData == (Keys.Right))
            {
                Right.PerformClick();
                return true;
            }
            if (keyData == (Keys.Enter))
            {
                button9.PerformClick();
                return true;
            }
            if (keyData == (Keys.Return))
            {
                Back.PerformClick();
                return true;
            }
            if (keyData == (Keys.Space))
            {
                textBox3.Focus();
                return true;
            }
            if (keyData == (Keys.RControlKey))
            {
                options.Focus();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }



        private void register_Click(object sender, EventArgs e)
        {

            string hostname = System.Windows.Forms.SystemInformation.ComputerName;
            string jsontosend = "{\"id\":13,\"method\":\"actRegister\",\"version\":\"1.0\",\"params\":[{\"clientid\":\"" + hostname + ":34c43339-af3d-40e7-b1b2-743331375368c\",\"nickname\":\"" + hostname + " (Mendel's APP)\"},[{\"clientid\":\"" + hostname + ":34c43339-af3d-40e7-b1b2-743331375368c\",\"value\":\"yes\",\"nickname\":\"" + hostname + " (Mendel's APP)\",\"function\":\"WOL\"}]]}";

            try
            {
                var httpWebRequest = (HttpWebRequest)WebRequest.Create(" http://"+test.ipadress+"/sony/accessControl");
                httpWebRequest.ContentType = "application/json";
                httpWebRequest.Method = "POST";
                httpWebRequest.AllowAutoRedirect = true;
                httpWebRequest.Timeout = 500;

                using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
                {
                    streamWriter.Write(jsontosend);
                }

                try
                {
                    httpWebRequest.GetResponse();
                }
                catch { }
            }
            catch { textBox1.Text += "device not reachable \r\n"; }

            RegisterForm tempregisterform = new RegisterForm();
            tempregisterform.ShowDialog();
            string pincode=tempregisterform.returnvalue;
            textBox1.Text += pincode;

           // string hostname = System.Windows.Forms.SystemInformation.ComputerName;
           // string jsontosend = "{\"id\":13,\"method\":\"actRegister\",\"version\":\"1.0\",\"params\":[{\"clientid\":\"" + hostname + ":34c43339-af3d-40e7-b1b2-743331375368c\",\"nickname\":\"" + hostname + " (Mendel's APP)\"},[{\"clientid\":\"" + hostname + ":34c43339-af3d-40e7-b1b2-743331375368c\",\"value\":\"yes\",\"nickname\":\"" + hostname + " (Mendel's APP)\",\"function\":\"WOL\"}]]}";

            try
            {
                var httpWebRequest2 = (HttpWebRequest)WebRequest.Create(" http://192.168.1.9/sony/accessControl");
                httpWebRequest2.ContentType = "application/json";
                httpWebRequest2.Method = "POST";
                httpWebRequest2.AllowAutoRedirect = true;
                httpWebRequest2.CookieContainer = allcookies;
                httpWebRequest2.Timeout = 500;

                using (var streamWriter = new StreamWriter(httpWebRequest2.GetRequestStream()))
                {
                    streamWriter.Write(jsontosend);
                }

                string authInfo = "" + ":" + pincode;
                authInfo = Convert.ToBase64String(Encoding.Default.GetBytes(authInfo));
                httpWebRequest2.Headers["Authorization"] = "Basic " + authInfo;

                var httpResponse = (HttpWebResponse)httpWebRequest2.GetResponse();


                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    var responseText = streamReader.ReadToEnd();
                    textBox1.Text += responseText + "\r\n";
                }

                //write register cookie to file!
                string answerCookie = JsonConvert.SerializeObject(httpWebRequest2.CookieContainer.GetCookies(new Uri("http://"+test.ipadress+"/sony/appControl")));

                // Write the string to a file.
                System.IO.StreamWriter file = new System.IO.StreamWriter("cookie.json");
                file.WriteLine(answerCookie);
                file.Close();

            }
            catch { textBox1.Text += "timeout \r\n"; }
        
        }

       //// private void button10_Click(object sender, EventArgs e)
       // {
       //     string hostname = System.Windows.Forms.SystemInformation.ComputerName;
       //     string jsontosend = "{\"id\":13,\"method\":\"actRegister\",\"version\":\"1.0\",\"params\":[{\"clientid\":\"" + hostname + ":34c43339-af3d-40e7-b1b2-743331375368c\",\"nickname\":\"" + hostname + " (Mendel's APP)\"},[{\"clientid\":\"" + hostname + ":34c43339-af3d-40e7-b1b2-743331375368c\",\"value\":\"yes\",\"nickname\":\"" + hostname + " (Mendel's APP)\",\"function\":\"WOL\"}]]}";

       //     var httpWebRequest = (HttpWebRequest)WebRequest.Create(" http://192.168.1.9/sony/accessControl");
       //     httpWebRequest.ContentType = "application/json";
       //     httpWebRequest.Method = "POST";
       //     httpWebRequest.AllowAutoRedirect = true;
       //     httpWebRequest.CookieContainer = allcookies;

       //     using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
       //     {
       //         streamWriter.Write(jsontosend);
       //     }

       //     string authInfo = "" + ":" + textBox4.Text;
       //     authInfo = Convert.ToBase64String(Encoding.Default.GetBytes(authInfo));
       //     httpWebRequest.Headers["Authorization"] = "Basic " + authInfo;

       //     var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();


       //     using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
       //     {
       //         var responseText = streamReader.ReadToEnd();
       //         textBox1.Text += responseText + "\r\n";
       //     }
       // }

        private void pictureBox1_MouseEnter(object sender, EventArgs e)
        {

        }

        private void Form1_Load_1(object sender, EventArgs e)
        {
            try
            {
                System.IO.StreamReader myFile = new System.IO.StreamReader("settings.json");
                string myString = myFile.ReadToEnd();
                myFile.Close();

                SonyBraviaClass newtest = JsonConvert.DeserializeObject<SonyBraviaClass>(myString);
                textBox2.Text = newtest.macadres;
            }
            catch { textBox1.Text += "configfile doesnt exists"; }


            try
            {
                textBox1.Text += "start find\r\n";
                Search(miTimeout, mbCheckForAction, mbVerboseUPnPLog);
                textBox1.Text += "done searching\r\n";

            }
            catch { textBox1.Text += "no sony found, try wakeing it! \r\n"; }
        }
    }
}
        #endregion