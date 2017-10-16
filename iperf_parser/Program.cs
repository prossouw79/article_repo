using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Text;

namespace iperf_parser
{
	class MainClass
	{
		public static void Main (string[] args)
		{
			string inPath = "";
			
			string adapter, router, distance;
			adapter = router = distance = "-";
            double linpack_time = 0;
			double linpack_MFlops = 0;

			List<string> lines_raw = new List<string> ();
			if (!args.Any ()) 
			{
				Console.WriteLine ("No file argument provided");
				Environment.Exit (0);
			} else {
				inPath = args [0];
			}
			if (!File.Exists (inPath)) {
                Console.WriteLine ("File not readable:\t"+inPath);
				Environment.Exit (0);
			} else {
				lines_raw = File.ReadLines (inPath).ToList();
			}

			if (lines_raw.Count == 0) {
                Console.WriteLine ("File is empty:\t"+ inPath);
				Environment.Exit (0);
			}
				
			string jobname = Path.GetFileNameWithoutExtension (inPath);

			if (jobname.Split ('_').Count() == 3) {
				string[] parts = jobname.Split ('_');

				distance = parts [0];
				adapter = parts [1];
				router = parts [2];
			}

			//List<string> lines_raw = new List<string> ();
			/*
			foreach (string l in lines_raw) {
				//filter lines of interest
				if (l.StartsWith ("#") || l.StartsWith ("[") || l.Contains("avg"))
					lines_filtered.Add (l);
			}*/

			List<experiment> lst_experiments = new List<experiment>();
            experiment tmp_ex = new experiment(-1);

			//new method
            for (int k = 0; k < lines_raw.Count; k++) {
				
				string cur_line = lines_raw [k];
				
                //Console.WriteLine("\t\t" + cur_line);

                bool header_trigger = (cur_line.Contains("TCP iperf3") || cur_line.Contains("UDP iperf3") || cur_line.Contains("fping latency"));
                bool fping_trigger_data = containsAllElements(cur_line, new string[] {"bytes","ms","avg","loss" });
                bool tcp_trigger_data = containsAllElements(cur_line, new string[] { "sec", "MBytes", "Mbits/sec", "sender" });
                bool udp_trigger_data = containsAllElements(cur_line, new string[] { "sec", "Bytes", "Mbits/sec", "ms" });
                bool linpack_trigger = containsAllElements(cur_line, new string[] { "T/V","N","NB","P","Q","Time","Gflops" });

                if (fping_trigger_data)
                {
					//Console.WriteLine("FPING DATA LINE:" + cur_line + "->" + getFilteredLine(cur_line, "0123456789. "));
					string filtered = getFilteredLine(cur_line, "0123456789. ");
                    string[] parts = filtered.Split(';');

                    double avg_latency = DParse(parts[4]);
                    tmp_ex.fping_latency = avg_latency;
                }

                if (tcp_trigger_data)
                {
					//Console.WriteLine("TCP DATA LINE:" + cur_line + "->" + getFilteredLine(cur_line, "0123456789. "));
					string filtered = getFilteredLine(cur_line, "0123456789. ");
					string[] parts = filtered.Split(';');

                    double avg_bandwidth = DParse(parts[3]);
                    int retransmissions = int.Parse(parts[4]);

                    tmp_ex.tcp_avg_bw = avg_bandwidth;
                    tmp_ex.tcp_retansmissions = retransmissions;
                }

                if (udp_trigger_data)
                {
					//Console.WriteLine("UDP DATA LINE:" + cur_line + "->" + getFilteredLine(cur_line, "0123456789. "));
					string filtered = getFilteredLine(cur_line, "0123456789. ");
					string[] parts = filtered.Split(';');

                    double tr_rate = DParse(parts[3]);
                    double jitter = DParse(parts[4]);
                    double ploss = DParse(parts[6]);

                    tmp_ex.udp_tr_rate = tr_rate;
                    tmp_ex.udp_jitter = jitter;
                    tmp_ex.udp_ploss_ratio = ploss;
                }


                if (header_trigger)
                {
                    string[] parts = lines_raw[k].Split(';');
                    int _id = int.Parse(parts[1]);
                    string _from = parts[3];
                    string _to = parts[4];
                    //Console.WriteLine("FROM " + _from + " TO " + _to);

                    if (tmp_ex.id > -1)//this test has been instantiated
                    {
                        if (tmp_ex.id != _id)//indicates this is a new experiment
                        {
                            lst_experiments.Add(tmp_ex); //add previous experiment to list
                            tmp_ex = new experiment(_id);//create new experiment object
                        }else{
                            //assign parsed vars
                            tmp_ex.sender = _from; 
                            tmp_ex.receiver = _to;
                        }
                    }else //experiment has not been instantiated
                    {
                        tmp_ex = new experiment(_id); //instantiate experiment
                    }
                }

                if(linpack_trigger)
                {
                    string dataline = getFilteredLine(lines_raw[k + 2],"0123456789. e-");
                    //Console.WriteLine(dataline);//6,7
                    string[] parts = dataline.Split(';');
                    linpack_time = DParse(parts[5]);
                    linpack_MFlops = DParse(parts[6])*1000;//convert to Mflops immediately
                    Console.WriteLine("Linpack: " + linpack_time.ToString("F2") + " : " + linpack_MFlops.ToString("F4"));
                }
			}
            lst_experiments.Add(tmp_ex);//add the last experiment


            string header = "ID;SNDER;RCVER;AVG_BW;AVG_LT;TCP_RTR;UDP_TRT;AVG_JTR;AVG_PLSS";
            string csv = header + Environment.NewLine;
            foreach(experiment ex in lst_experiments)
            {
                csv += ex.getCSVLine() + Environment.NewLine;
            }

            List<double> values_bw = lst_experiments.Select(ex => ex.tcp_avg_bw).ToList();
            List<double> values_lt = lst_experiments.Select(ex => ex.fping_latency).ToList();
            List<double> values_rt = lst_experiments.Select(ex => (double) ex.tcp_retansmissions).ToList();
            List<double> values_tr = lst_experiments.Select(ex => ex.udp_tr_rate).ToList();
            List<double> values_jt = lst_experiments.Select(ex => ex.udp_jitter).ToList();
            List<double> values_pl = lst_experiments.Select(ex => ex.udp_ploss_ratio).ToList();
            string footer = "-;-;-;-;-;-;-;-;-;";
            footer += Environment.NewLine +  "-;-;-;" + stats.Mean(values_bw).ToString("F2") +";"+ stats.Mean(values_lt).ToString("F2") + ";" + stats.Mean(values_rt).ToString("F2") + ";" + stats.Mean(values_tr).ToString("F2") + ";" + stats.Mean(values_jt).ToString("F2") + ";" + stats.Mean(values_pl).ToString("F2")+";AVG/MEAN";
            footer += Environment.NewLine +  "-;-;-;" + stats.StandardDeviation(values_bw).ToString("F2") + ";" + stats.StandardDeviation(values_lt).ToString("F2") + ";" + stats.StandardDeviation(values_rt).ToString("F2") + ";" + stats.StandardDeviation(values_tr).ToString("F2") + ";" + stats.StandardDeviation(values_jt).ToString("F2") + ";" + stats.StandardDeviation(values_pl).ToString("F2") + ";STANDARD DEVIATION";
            footer += Environment.NewLine + "-;-;-;" + stats.Variance(values_bw).ToString("F2") + ";" + stats.Variance(values_lt).ToString("F2") + ";" + stats.Variance(values_rt).ToString("F2") + ";" + stats.Variance(values_tr).ToString("F2") + ";" + stats.Variance(values_jt).ToString("F2") + ";" + stats.Variance(values_pl).ToString("F2") + ";VARIANCE";
            csv += footer;

			csv = csv.Replace ("192.168.0.101", "RP1");
			csv = csv.Replace ("192.168.0.201", "RP1");
			csv = csv.Replace ("192.168.0.102", "RP2");
			csv = csv.Replace ("192.168.0.202", "RP2");
			csv = csv.Replace ("192.168.0.103", "RP3");
			csv = csv.Replace ("192.168.0.203", "RP3");

            csv += Environment.NewLine + "-;-;-;-;-;-;-;-;-;";
            csv += Environment.NewLine + "Linpack (MFlops);" + linpack_MFlops;
			csv += Environment.NewLine + "Time (s)\t;" + linpack_time;

			Console.WriteLine(csv.Replace(";", "\t"));

			File.WriteAllText (jobname + ".csv", csv);
            Console.WriteLine("Written to: " + jobname + ".csv");
		}

        static string getFilteredLine(string _lineIn, string allowedChars)
        {
            string tmp = new string(_lineIn.Where(c => allowedChars.ToCharArray().Contains(c)).ToArray());
            tmp = tmp.Replace(" ", ";");
            while (tmp.Contains(";;"))
                tmp = tmp.Replace(";;", ";");

            return tmp.Trim(new char[] {';',' '});
        }

		//region agnostic double parsing
		static double DParse(string input)
		{
			double tmp = 0;
			double.TryParse (input, System.Globalization.NumberStyles.Any, CultureInfo.InvariantCulture, out tmp);
			return tmp;
			/* alternative method that also seems to work
			if (double.TryParse (input.Replace ('.', ','), out tmp))
				return tmp;*/
		}

		static string getDelimitedNumbers(string line_in,string goodChars = "0123456789. ")
		{
			string tmp = line_in;
			goodChars.ToCharArray();
			tmp = new String(tmp.Where(c => goodChars.Any(n => n == c)).ToArray());

			tmp = tmp.Trim().Replace (" ", ";");

			while (tmp.Contains (";;"))
				tmp = tmp.Replace (";;", ";");

			return tmp;
		}

        static bool containsAllElements(string line, string[] requirements)
        {
            bool tmp = true;

            foreach (string s in requirements)
                if (!line.Contains(s))
                    tmp = false;

            return tmp;
        }
	}
		
    class experiment
    {
        //basic vars
		public int id = -1;
        //public string type;
		public string sender, receiver;
        //public string adapter, router, distance;

        //tcp_specific vars
        public double tcp_avg_bw;
        public int tcp_retansmissions;

        //udp specific vars
        public double udp_tr_rate, udp_jitter, udp_ploss_ratio;

        //fping vars
        public double fping_latency;

        public experiment(int _id)
        {
            this.id = _id;
        }


        public string getCSVLine()
        {
            string dl = ";";
            return id + dl + sender + dl + receiver + dl + tcp_avg_bw + dl + fping_latency + dl + tcp_retansmissions + dl + udp_tr_rate + dl + udp_jitter + dl + udp_ploss_ratio;
        }
    }

	static class stats
	{
		public static double Mean(this List<double> values)
		{
			return values.Count == 0 ? 0 : values.Mean(0, values.Count);
		}

		public static double Mean(this List<double> values, int start, int end)
		{
			double s = 0;

			for (int i = start; i < end; i++)
			{
				s += values[i];
			}

			return s / (end - start);
		}

		public static double Variance(this List<double> values)
		{
			return values.Variance(values.Mean(), 0, values.Count);
		}

		public static double Variance(this List<double> values, double mean)
		{
			return values.Variance(mean, 0, values.Count);
		}

		public static double Variance(this List<double> values, double mean, int start, int end)
		{
			double variance = 0;

			for (int i = start; i < end; i++)
			{
				variance += Math.Pow((values[i] - mean), 2);
			}

			int n = end - start;
			if (start > 0) n -= 1;

			return variance / (n);
		}

		public static double StandardDeviation(this List<double> values)
		{
			return values.Count == 0 ? 0 : values.StandardDeviation(0, values.Count);
		}

		public static double StandardDeviation(this List<double> values, int start, int end)
		{
			double mean = values.Mean(start, end);
			double variance = values.Variance(mean, start, end);

			return Math.Sqrt(variance);
		}
	}
}
