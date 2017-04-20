import struct
from array import array

def csvToBin(filenameIn, filenameOut):
    csvFile = open(filenameIn, "r")
    binFile = open(filenameOut, "wb+")
    
    maxMagnitude = 1000.0
	
    for line in csvFile:
        value = (float(line) / maxMagnitude) * 255.0
        data = struct.pack('B',value) #pack values as binary byte 
        #print struct.unpack('B', data)
        binFile.write(data)
    binFile.close()
    csvFile.close()

    #binFile = open(filenameOut, 'rb')
    #float_array = array('f')
    #float_array.fromstring(binFile.read())
    #print float_array
    #binFile.close()
    
#filenameIn = "fireAtrium0.0.csv"
#filenameOut = "fireAtrium0.0.bin"
#csvToBin(filenameIn, filenameOut)

filepath = "../Assets/Resources/AtriumData/"

for i in range(40+1):
    filenameIn = filepath + "fireAtrium0." + str(i) + ".csv"
    filenameOut = filepath + "fireAtrium0." + str(i) + ".bytes"
    csvToBin(filenameIn, filenameOut)
