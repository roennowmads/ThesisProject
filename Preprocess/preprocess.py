import math
import os, os.path
import struct

maxMag = float("-inf")
minMag = float("inf")

array = {}

def processFilePositions(dir, filename):
    fIn = open(dir + "/" + filename)
    filenameNoExt = os.path.splitext(filename)[0]
    
    fOut = open(dir + "/output/" + filenameNoExt + ".pos.bytes", "wb+")

    #skip first line
    fIn.readline()	
    for i, line in enumerate(fIn):
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

        if (i % 100000) == 0:
            print "Positions processed:", i
        
    fIn.close()
    fOut.close()

def processFileValues(dir, filename):
    fIn = open(dir + "/" + filename)
    filenameNoExt = os.path.splitext(filename)[0]
    fOut = open(dir + "/output/" + filenameNoExt + ".bytes", "wb+")

    outString = ""
    
    minMagnitude = -7.9246e-07
    maxMagnitude = 0.0010371  # Only correct for Visibility property

    #skip first line
    fIn.readline()	
    for line in fIn:
        strippedLined = line.strip()
        lines = strippedLined.split(',')
        x = float(lines[0])
        
        global maxMag
        if maxMag < x:
            maxMag = x
        
        global minMag
        if minMag > x:
            minMag = x
        
        #print x
        value = int(((x - minMagnitude) / (maxMagnitude - minMagnitude)) * 255.0)
        #print value
        #global array
        #if array.has_key(value):
        #    array[value] += 1
        #else:
        #    array[value] = 1
        #print value
        
        #global maxMag
        #if maxMag < value:
        #    maxMag = value
        
        #global minMag
        #if minMag > value:
        #    minMag = value
        
        
        data = struct.pack('B',value) #pack values as binary byte 
        
        fOut.write(data)
        
    fIn.close()
    fOut.close()
	

dir = 'C:\Users\madsr\Desktop\Atrium Data\output'
filenames = [name for name in os.listdir(dir) if os.path.isfile(os.path.join(dir, name))]
numFiles = len(filenames)

processFilePositions(dir, filenames[0])

for filename in filenames:
    processFileValues(dir, filename)
	
print minMag, maxMag
print array