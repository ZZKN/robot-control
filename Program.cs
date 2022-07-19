using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace psi {

    class Server {
        const int port = 8080;

        static void Main(string[] args) {
            TcpListener listener = null;

            try {
                listener = new TcpListener(IPAddress.Parse("127.0.0.1"), port);
                listener.Start();
                Console.WriteLine("Server started...");
                while (true) {
                    TcpClient client = listener.AcceptTcpClient();
                    Robot robot = new Robot(client);
                    Console.WriteLine("Accepted new client connection...");
                    Thread t = new Thread(robot.begin);
                    t.Start();
                }

            } catch (Exception e) {
                Console.WriteLine(e);
            } finally {
                if ( listener != null){
                    listener.Stop();
                }
            }

        }
    }

    class Point
    {
        public Point(int posX, int posY)
        {
            PosX = posX;
            PosY = posY;
        }

        public int PosX { get; set; }
        public int PosY { get; set; }

        public bool Equals(Point otherPoint)
        {
            return PosX == otherPoint.PosX && PosY == otherPoint.PosY;
        }

        public bool Equals(int x, int y)
        {
            return PosX == x && PosY == y;
        }
    }

    class Robot {
        public enum Direction { UP, RIGHT, DOWN, LEFT };
        const string forward = "102 MOVE\a\b";
        const string left = "103 TURN LEFT\a\b";
        const string right = "104 TURN RIGHT\a\b";
        const string pickup = "105 GET MESSAGE\a\b";
        const string logout = "106 LOGOUT\a\b";
        const string keyReq = "107 KEY REQUEST\a\b";
        const string allgood = "200 OK\a\b";
        const string loginFail = "300 LOGIN FAILED\a\b";
        const string syntaxErr = "301 SYNTAX ERROR\a\b";
        const string logicErr = "302 LOGIC ERROR\a\b";
        const string keyOFR = "303 KEY OUT OF RANGE\a\b";
        const int TIMEOUT = 1;
        const int TIMEOUT_RECHARGING = 5;

        public TcpClient client;

        NetworkStream reader;
        byte[] receiveBuffer;
        int bytesReceived;

        public static int[] serverKey = { 23019, 32037, 18789, 16443, 18189 };
        public static int[] clientKey = { 32037, 29295, 13603, 29533, 21952 };
        public Queue<string> repliesQueue = new Queue<string>();
        Point coord;
        Direction dir;

        public Robot(TcpClient client) {
            this.client = client;

            client.ReceiveTimeout = TIMEOUT*1000;
        }

        public void begin()
        {
            Console.WriteLine("Send id={0}: {1}", Thread.CurrentThread.ManagedThreadId, "Robot is alive");

            try
            {
                if (authorisation())
                {
                    Console.WriteLine("Send id={0}: {1}", Thread.CurrentThread.ManagedThreadId, "Starting movement...");
                    if (!navigate()) Console.WriteLine("Send id={0}: {1}", Thread.CurrentThread.ManagedThreadId, "Could not find message");
                    client.Close();
                }


                client.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                client.Close();
            }

            Console.WriteLine("Send id={0}: {1}", Thread.CurrentThread.ManagedThreadId, "closed");


        }

        bool authorisation() {

            reader = client.GetStream();
            receiveBuffer = new byte[client.ReceiveBufferSize];
            string msg = "";
            string name = "";
            int hash = 0;

            while ( repliesQueue.Count == 0) 
                if (!listen(20)) return false;

            // get name
            name = repliesQueue.Dequeue();
            Console.WriteLine("robot name: {0}, length: {1}", name, name.Length);

            // request keyID
            sendMessage(keyReq);

            // get keyID
            while (repliesQueue.Count == 0)
                if (!listen(5)) return false;
            msg = repliesQueue.Dequeue();

            // check if keyID is a number
            if (!Regex.IsMatch(msg, @"^\d+$")) {
                sendMessage(syntaxErr);
                return false; 
            }

            int keyID = int.Parse(msg.ToString());
            Console.WriteLine("Debug: keyID = {0}", keyID);
            
            // check keyID range
            if (keyID >= 5 || keyID < 0) {
                sendMessage(keyOFR);
                return false; 
            }

            // calculate hash
            foreach (byte b in name)
            {
                hash += b;
            }
            int tmp = (((hash * 1000) % 65536) + serverKey[keyID] ) % 65536;
            sendMessage(tmp.ToString()+"\a\b");
            hash = (((hash * 1000) % 65536) + clientKey[keyID]) % 65536;

            // get hash from client
            while (repliesQueue.Count == 0)
            {
                if (!listen(7)) return false;
            }
            msg = repliesQueue.Dequeue();
            int confhash = int.Parse(msg);

            // check if hash is only numbers
            if (!Regex.IsMatch(msg, @"^\d+$"))
            {
                sendMessage(syntaxErr);
                return false;
            }

            // confirm validity
            if (confhash != hash)
            {
                sendMessage(loginFail);
                return false;
            } else if (confhash == hash) { sendMessage(allgood); }

            return true;
        }

        bool navigate()
        {
            // determine starting position and direction of robot
            if (!startPos()) return false;
            Point tmpcoord;

            while (!coord.Equals(0, 0))
            {
                tmpcoord = coord;
                // move from the right side of the grid towards the center
                if (coord.PosX > 0)
                {
                    while (dir != Direction.LEFT)
                    {
                        if (!moveComand(right)) return false;
                        dir = (Direction)(((int)dir + 1) % 4);
                    }

                    if (!moveComand(forward)) return false;
                    if (coord.Equals(tmpcoord))
                    {
                        if (coord.PosY >= 0) { upAndAround(); }
                        else { downAndAround(); }
                    }


                } // move from the left side of the grid towards the center
                else if (coord.PosX < 0)
                {
                    while (dir != Direction.RIGHT)
                    {
                        if (!moveComand(right)) return false;
                        dir = (Direction)(((int)dir + 1) % 4);
                    }

                    if (!moveComand(forward)) return false;
                    if (coord.Equals(tmpcoord))
                    {
                        if (coord.PosY < 0) { upAndAround(); }
                        else { downAndAround(); }

                    }
                }
                // posX == 0, move straight ahead
                else {   
                    if (coord.PosY > 0)
                    {
                        // in the upper half turn towards center
                        while (dir != Direction.DOWN)
                        {
                            if (!moveComand(right)) return false;
                            dir = (Direction)(((int)dir + 1) % 4);
                        }
                    }
                    else
                    {
                        // in the lower half turn towards center
                        while (dir != Direction.UP)
                        {
                            if (!moveComand(right)) return false;
                            dir = (Direction)(((int)dir + 1) % 4);
                        }
                    }

                    if (!moveComand(forward)) return false;

                    // if [0,y] is inaccessible go around it and settle in the same position just two cells ahead
                    if (coord.Equals(tmpcoord))
                    {
                        if (coord.PosY > 0)
                        {
                            if (!moveComand(right)) return false;
                        }
                        else { if (!moveComand(left)) return false; }

                        if (!moveComand(forward)) return false;

                        if (coord.PosY > 0)
                        {
                            if (!moveComand(left)) return false;
                        }
                        else { if (!moveComand(right)) return false; }

                        if (!moveComand(forward)) return false;
                        if (!moveComand(forward)) return false;

                        if (coord.PosY > 0)
                        {
                            if (!moveComand(left)) return false;
                        }
                        else { if (!moveComand(right)) return false; }

                        if (!moveComand(forward)) return false;

                        if (coord.PosY > 0)
                        {
                            if (!moveComand(right)) return false;
                        }
                        else { if (!moveComand(left)) return false; }

                    }

                }
            }

            // at destination [0,0] retrieve the msg
            sendMessage(pickup);
            while (repliesQueue.Count == 0)
                if (!listen(100)) return false;
            Console.WriteLine(repliesQueue.Dequeue());
            sendMessage(logout);
            return true;
        }

        bool moveComand(string cmd)
        {
            sendMessage(cmd);
            while (repliesQueue.Count == 0)
                if (!listen(12)) return false;

            string tmp = repliesQueue.Dequeue();
            string[] res = tmp.Split(' ');
            int n;
            if (res[0] != "OK" || !int.TryParse(res[1], out n) || !int.TryParse(res[2], out n) || res.Length > 3)
            {
                sendMessage(syntaxErr);
                return false;
            }
            Console.WriteLine("Send id={0}: {1}", Thread.CurrentThread.ManagedThreadId, tmp);
            coord = new Point(int.Parse(res[1]), int.Parse(res[2]));

            return true;
        }

        bool startPos()
        {
            // first set of coordinates
            if (!moveComand(forward)) return false;
            Point tmp = coord;
            // second set of coordinates
            if (!moveComand(forward)) return false;
            // in case of no movement turn right and forward
            if(coord.Equals(tmp))
            {
                tmp = coord;
                if (!moveComand(right)) return false;
                if (!moveComand(forward)) return false;  //if it cant move again its killed off
            }

            // given two sets of coordinates determine the direction of the robot
            if(coord.PosX == tmp.PosX)
            {
                if (coord.PosY < tmp.PosY) dir = Direction.DOWN;
                else dir = Direction.UP;
            } else if (coord.PosX > tmp.PosX)
            {
                dir = Direction.RIGHT;
            } else
            {
                dir = Direction.LEFT;
            }

            return true;
        }

        bool upAndAround()
        {
            if (!moveComand(right)) return false;
            if (!moveComand(forward)) return false;
            if (!moveComand(left)) return false;
            if (!moveComand(forward)) return false;
            return true;
        }

        bool downAndAround()
        {
            if (!moveComand(left)) return false;
            if (!moveComand(forward)) return false;
            if (!moveComand(right)) return false;
            if (!moveComand(forward)) return false;
            return true;
        }

        void sendMessage(string message)
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            Console.WriteLine("Send id={0}: {1}", Thread.CurrentThread.ManagedThreadId, message);
            reader.Write(Encoding.ASCII.GetBytes(message),0,data.Length);
        }

        bool listen(int length)
        {
            string leftovers = "";
            string msg = "";
            DateTime t1= DateTime.Now;
            DateTime t2;

            while (true)
            {
                t2 = t1;
                t1 = DateTime.Now;
                msg = "";

                // track time between unfinshed responses
                TimeSpan diff = t1 - t2;
                if (diff.TotalSeconds > TIMEOUT)
                {
                    sendMessage(logout);
                    return false;
                }

                // get reply
                bytesReceived = reader.Read(receiveBuffer, 0, receiveBuffer.Length);
                msg = Encoding.ASCII.GetString(receiveBuffer, 0, bytesReceived);
                
                // empty msg or the end of unfinished response
                if(msg.Length == 0)
                {
                    if (leftovers.Length == 0) sendMessage(syntaxErr);
                    else sendMessage(logout);

                    return false;
                }

                //paste together anything that was read before
                msg = leftovers + msg;
                string[] msgs = msg.Split(new string[] { "\a\b" }, StringSplitOptions.None);
                int len = msgs[0].Length;
                string first = msgs[0];

                // reply is too long but can also be unfinished, if it ends with \a it runs another cycle 
                if (len > length - 2 && msgs[0] != "RECHARGING" && !(len == length - 1 && first[len - 1] == 7))
                {
                    sendMessage(syntaxErr);
                    return false;
                }

                // received full power without recharging 
                if (msg.Contains("FULL POWER"))
                {
                    sendMessage(logicErr);
                    return false;
                }

                if (msg.Contains("RECHARGING\a\b"))
                {
                    client.ReceiveTimeout = TIMEOUT_RECHARGING * 1000;
                    while (true)
                    {
                        if(! client.Connected)
                        {
                            sendMessage(logout);
                            return false;
                        }
                        //Console.WriteLine(DateTime.Now);
                        bytesReceived = reader.Read(receiveBuffer, 0, receiveBuffer.Length);
                        //Console.WriteLine(DateTime.Now);
                        string tmp = Encoding.ASCII.GetString(receiveBuffer, 0, bytesReceived);
                        string[] replies = tmp.Split(new string[] { "\a\b" }, StringSplitOptions.None);
                        // reply is anything but full power
                        if ( !tmp.Contains("FULL POWER\a\b") && tmp.Length > 0)
                        {
                            Console.WriteLine("TimeOut done, received: {0}", tmp);
                            sendMessage(logicErr);
                            return false;
                        } else if (tmp.Contains("FULL POWER\a\b"))
                        {
                            if (replies[0] != "FULL POWER")
                            {
                                sendMessage(logicErr);
                                return false;
                            }
                            // reply could once again be unfinished 
                            if (replies.Count() > 1)
                            {
                                msg = msg + tmp;
                                msgs = msg.Split(new string[] { "\a\b" }, StringSplitOptions.None);
                            }
                            break;
                        }
                    }

                    client.ReceiveTimeout = TIMEOUT*1000;
                }

                //ends with correct suffix
                if (msg.Length >= 2 && msg.Substring(msg.Length - 2) == "\a\b")
                {
                    // enqueue all usefull replies
                    foreach (string reply in msgs)
                    {
                        if(reply != "RECHARGING" && reply != "FULL POWER" && reply != "") repliesQueue.Enqueue(reply);
                    }

                    return true;

                } else
                {//unfinished reply runs another cycle
                    leftovers = msg;
                }

            }

        }


    }
}
