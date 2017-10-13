#!/bin/bash
target_dir="article_results"
mkdir $target_dir -p

#Physical setup
suffix="atlantis"
distance="CLOSE"
adapter="RPI" #CISCO/RPI
router="TRENDNET" #RPI/TRENDNET/ASUS
test_time=${1:-10} #time to run test in seconds, default 10
echo "Test time set to " $test_time " seconds"

outfile=$target_dir"/"$distance"_"$adapter"_"$router"_"$suffix".txt"
filename=$distance"_"$adapter"_"$router"_"$suffix".txt"

iperf_tcp_args="-i $test_time -f m -R -t $test_time"
iperf_udp_args="-u -i $test_time -f m -t $test_time"

server_start="iperf3 -s"

#manage adapters (only one can be active at a time for consistent results)
echo "Configuring NICs"
if [ "$adapter" = "CISCO" ]
then
	machinefile="nodes_cisco"
	echo "TURNING OFF BCM ADAPTERS FOR CISCO NIC TEST"
	mpirun -f $machinefile sh ~/netSwitch/BCM_off.sh
	mpirun -f $machinefile sh ~/netSwitch/CISCO_on.sh
fi

if [ "$adapter" = "RPI" ]
then
	machinefile="nodes_wifi"
	echo "TURNING OFF CISCO ADAPTERS FOR BCM NIC TEST"
	mpirun -f $machinefile sh ~/netSwitch/CISCO_off.sh
	mpirun -f $machinefile sh ~/netSwitch/BCM_on.sh
fi

#kill any existing iperf3 servers
echo "Killing running iperf3 servers"
mpirun -f $machinefile pkill iperf3
sleep 1

#start iperf3 servers in TCP mode
echo "Starting new TCP iperf3 servers"
mpirun -f $machinefile $server_start &
sleep 1

echo "Waiting 5 seconds for network..."
#sleep 5
echo "Continuing..."

echo 'Output file:' "$outfile"

rm $outfile

COUNTER=1
wifi_lines=`cat $machinefile`
for wifi_ip_1 in $wifi_lines
	do
		for wifi_ip_2 in $wifi_lines
		 do
					if [ "$wifi_ip_1" != "$wifi_ip_2" ]
					then
					  echo $COUNTER
						echo "###############################################################" >> $outfile
						echo "Running FPING LATENCY TEST..."
						echo "#;"$COUNTER";fping latency;"$wifi_ip_1";"$wifi_ip_2 >> $outfile
						mpirun -hosts "$wifi_ip_1" fping -c $test_time $wifi_ip_2 | tee -a $outfile

						echo "Running TCP BANDWIDTH TEST..."
						echo "#;"$COUNTER";TCP iperf3;"$wifi_ip_1";"$wifi_ip_2";"$test_time >> $outfile
						mpirun -hosts "$wifi_ip_1" iperf3 -c $wifi_ip_2 $iperf_tcp_args $test_time | tee -a $outfile

						echo "Running UDP BANDWIDTH TEST"
						echo "#;"$COUNTER";UDP iperf3;"$wifi_ip_1";"$wifi_ip_2";"$test_time >> $outfile
						mpirun -hosts "$wifi_ip_1" iperf3 -c $wifi_ip_2 $iperf_udp_args | tee -a $outfile

						COUNTER=$((COUNTER + 1))
					fi
		done
	done
cat $outfile

#compile parser & run on dataset
cd iperf_parser
mcs Program.cs
mv Program.exe ../$target_dir
cd ../$target_dir
mono Program.exe $filename
