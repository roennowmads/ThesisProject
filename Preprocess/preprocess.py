import math
import os, os.path


maxMag = 0.0

def processFilePositions(dir):
	fIn = open(dir + "/foo0.0.csv")
	fOut = open(dir + "/output/foo0.pos.csv", "w")

	outString = ""
	
	#skip first line
	fIn.readline()	
	for line in fIn:
		strippedLined = line.strip()
		lines = strippedLined.split(',')
		x = lines[3]
		y = lines[4]
		z = lines[5]
		
		outString += x + "," + y + "," + z + "\n"
		

	fOut.write(outString[:-1])  #avoid including the last newline character
		
	fIn.close()
	fOut.close()

def processFileValues(dir, index):
	strIndex = str(index)

	fIn = open(dir + "/foo0." + strIndex + ".csv")
	fOut = open(dir + "/output/foo0." + strIndex + ".csv", "w")

	outString = ""
	
	#skip first line
	fIn.readline()	
	for line in fIn:
		strippedLined = line.strip()
		lines = strippedLined.split(',')
		x = float(lines[0])
		y = float(lines[1])
		z = float(lines[2])
		
		magnitude = math.sqrt(x*x+y*y+z*z)
		
		global maxMag
		if magnitude > maxMag:
			maxMag = magnitude
		
		if magnitude == 0.0:
			outString += "{0:.0f}".format(magnitude) + "\n"
		else:
			outString += "{0:.6f}".format(magnitude) + "\n"
		

	fOut.write(outString[:-1])  #avoid including the last newline character
		
	fIn.close()
	fOut.close()
	

dir = 'C:/Users/madsr/Desktop/transient_data'
numFiles = len([name for name in os.listdir(dir) if os.path.isfile(os.path.join(dir, name))])

processFilePositions(dir)

for i in range(numFiles):
	processFileValues(dir, i)
	
print maxMag