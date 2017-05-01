import math
import os, os.path
import struct

def processFilePositions(dir, filename, valuePerLine):
    fIn = open(dir + "/" + filename)
    filenameNoExt = os.path.splitext(filename)[0]
    
    fOut = open(dir + "/output/" + filenameNoExt + ".pos.bytes", "wb+")

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
            

        if (i % 100000) == 0:
            print "Positions processed:", i
        
    fIn.close()
    fOut.close()
    print numberOfFinalPoints


def findInterestingFileValues(dir, filename, valuePerLine, fileData):
    fIn = open(dir + "/" + filename)

    #skip first line
    fIn.readline()	
    for i, line in enumerate(fIn):
        strippedLined = line.strip()
        lines = strippedLined.split(',')
        x = float(lines[0])
        
        value = int(((x - minMagnitude) / (maxMagnitude - minMagnitude)) * 255.0)
        fileData.append(value)
        
        valuePerLine[i] += value        
    fIn.close()
	

def processFileValues(dir, filename, valuePerLine, fileData):
    filenameNoExt = os.path.splitext(filename)[0]
    fOut = open(dir + "/output/" + filenameNoExt + ".bytes", "wb+")
    for i, value in enumerate(fileData):
        if valuePerLine[i] <> 0:
            data = struct.pack('B', value) #pack values as binary byte 
            fOut.write(data)
    fOut.close()
    
    
def runPreprocess(directory, minMag, maxMag, numberOfPoints):
    dir = directory + '\output'
    filenames = [name for name in os.listdir(dir) if os.path.isfile(os.path.join(dir, name))]
    numFiles = len(filenames)
    valuePerLine = [0] * numberOfPoints
    
    global maxMagnitude
    maxMagnitude = maxMag
    
    global minMagnitude
    minMagnitude = minMag

    print ""
    print "Finding permanent zeroes in values data..."
    filesData = []
    for filename in filenames:
        fileData = []
        findInterestingFileValues(dir, filename, valuePerLine, fileData)
        print filename + " processed"
        filesData.append(fileData)
        
    print ""
    print "Creating binary files..."
    for i, filename in enumerate(filenames):
        fileData = filesData[i]
        processFileValues(dir, filename, valuePerLine, fileData)
        print filename + " processed"
        
    print ""
    print "Processing positions..."
    processFilePositions(dir, filenames[0], valuePerLine)
        
    print minMagnitude, maxMagnitude
