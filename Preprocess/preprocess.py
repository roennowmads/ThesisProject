import math
import os, os.path


maxMag = 0.0


def processFilePositions(dir, name):
	fIn = open(dir + "/" + name + "0.0.csv")
	fOut = open(dir + "/output/"+ name + "0.pos.csv", "w")

	outString = ""
	
	#skip first line
	fIn.readline()	
	for line in fIn:
		strippedLined = line.strip()
		lines = strippedLined.split(',')
		x = lines[1]
		y = lines[2]
		z = lines[3]
		
		outString += x + "," + y + "," + z + "\n"
		

	fOut.write(outString[:-1])  #avoid including the last newline character
		
	fIn.close()
	fOut.close()

def processFileValues(dir, name, index):
	strIndex = str(index)

	fIn = open(dir + "/" + name + "0." + strIndex + ".csv")
	fOut = open(dir + "/output/" + name + "0." + strIndex + ".csv", "w")

	outString = ""
	
	#skip first line
	fIn.readline()	
	for line in fIn:
		strippedLined = line.strip()
		lines = strippedLined.split(',')
		x = float(lines[0])
		
		outString += lines[0] + "\n"
		

	fOut.write(outString[:-1])  #avoid including the last newline character
		
	fIn.close()
	fOut.close()
	

modelName = "fireAtrium"
dir = 'C:/Users/madsr/Desktop/FireAtriumCSV'
numFiles = len([name for name in os.listdir(dir) if os.path.isfile(os.path.join(dir, name))])

processFilePositions(dir, modelName)

for i in range(numFiles):
	processFileValues(dir, modelName, i)
	
print maxMag