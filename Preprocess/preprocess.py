import math
import os, os.path
import struct

maxMag = 0.0


def processFilePositions(dir, filename):
    fIn = open(dir + "/" + filename)
    filenameNoExt = os.path.splitext(filename)[0]
    
    fOut = open(dir + "/output/" + filenameNoExt + ".pos.csv", "w")
    
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

def processFileValues(dir, filename):

    fIn = open(dir + "/" + filename)
    filenameNoExt = os.path.splitext(filename)[0]
    fOut = open(dir + "/output/" + filenameNoExt + ".bytes", "wb+")

    outString = ""
    
    maxMagnitude = 1000.0  # Only correct for Visibility property

    #skip first line
    fIn.readline()	
    for line in fIn:
        strippedLined = line.strip()
        lines = strippedLined.split(',')
        x = float(lines[0])
        
        value = (x / maxMagnitude) * 255.0
        data = struct.pack('B',value) #pack values as binary byte 
        
        fOut.write(data)
        
    fIn.close()
    fOut.close()
	

dir = 'G:/prep/atrium data part 2/output'
filenames = [name for name in os.listdir(dir) if os.path.isfile(os.path.join(dir, name))]
numFiles = len(filenames)

processFilePositions(dir, filenames[0])

for filename in filenames:
    processFileValues(dir, filename)
	
print maxMag