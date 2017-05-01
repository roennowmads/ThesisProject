import math
import os, os.path
import struct

maxMagnitude = float("-inf")
minMagnitude = float("inf")

array = {}

def processFilePositions(dir, filename, valuePerLine):
    fIn = open(dir + "/" + filename)
    filenameNoExt = os.path.splitext(filename)[0]
    
    fOut = open(dir + "/output/" + filenameNoExt + ".pos.bytes", "wb+")

    #print valuePerLine
    numberOfFinalPoints = 0
    
    #skip first line
    fIn.readline()	
    for i, line in enumerate(fIn):
        if valuePerLine[i] <> 0:
            strippedLined = line.strip()
            lines = strippedLined.split(',')
            x = float(lines[1])
            y = float(lines[2])
            z = float(lines[3])

            dataX = struct.pack('f',x)
            dataY = struct.pack('f',y)
            dataZ = struct.pack('f',z)

            fOut.write(dataX)
            fOut.write(dataY)
            fOut.write(dataZ)
            numberOfFinalPoints += 1
            

        #if (i % 100000) == 0:
        #print "Positions processed:", i
        
    fIn.close()
    fOut.close()
    print numberOfFinalPoints

	
def findMinAndMaxValues(dir, filename, valuePerLine):
    fIn = open(dir + "/" + filename)
    #skip first line
    fIn.readline()	
    for line in fIn:
        strippedLined = line.strip()
        lines = strippedLined.split(',')
        x = float(lines[0])

        global maxMagnitude
        if maxMagnitude < x:
            maxMagnitude = x

        global minMagnitude
        if minMagnitude > x:
            minMagnitude = x
            
        valuePerLine.append(0)
    fIn.close()


def findInterestingFileValues(dir, filename, valuePerLine):
    fIn = open(dir + "/" + filename)

    #skip first line
    fIn.readline()	
    for i, line in enumerate(fIn):
        strippedLined = line.strip()
        lines = strippedLined.split(',')
        x = float(lines[0])
        
        value = int(((x - minMagnitude) / (maxMagnitude - minMagnitude)) * 255.0)
        
        valuePerLine[i] += value
    fIn.close()
	

def processFileValues(dir, filename, valuePerLine):
    fIn = open(dir + "/" + filename)
    filenameNoExt = os.path.splitext(filename)[0]
    fOut = open(dir + "/output/" + filenameNoExt + ".bytes", "wb+")
    
    #skip first line
    fIn.readline()	
    for i, line in enumerate(fIn):
        if valuePerLine[i] <> 0:
            strippedLined = line.strip()
            lines = strippedLined.split(',')
            x = float(lines[0])
            
            #print x
            value = int(((x - minMagnitude) / (maxMagnitude - minMagnitude)) * 255.0)
            
            data = struct.pack('B', value) #pack values as binary byte 
            
            fOut.write(data)
            #print valuePerLine
        
    fIn.close()
    fOut.close()
    
dir = 'C:\Users\madsr\Desktop\Atrium Data\output'
filenames = [name for name in os.listdir(dir) if os.path.isfile(os.path.join(dir, name))]
numFiles = len(filenames)
valuePerLine = [] 

print "Find min and max values..."
for filename in filenames:
    findMinAndMaxValues(dir, filename, valuePerLine)
    print filename + " processed"


print ""
print "Finding permanent zeroes in values data..."
for filename in filenames:
    findInterestingFileValues(dir, filename, valuePerLine)
    print filename + " processed"
    
print ""
print "Creating binary files..."
for filename in filenames:
    processFileValues(dir, filename, valuePerLine)
    print filename + " processed"
	
print ""
print "Processing positions..."
processFilePositions(dir, filenames[0], valuePerLine)
    
print minMagnitude, maxMagnitude
#print array