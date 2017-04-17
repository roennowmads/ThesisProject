import struct
from array import array

def csvToBin(filenameIn, filenameOut):
    csvFile = open(filenameIn, "r")
    binFile = open(filenameOut, "wb+")
    
    for line in csvFile:
        value = float(line)
        data = struct.pack('f',value)
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



for i in range(40+1):
    filenameIn = "fireAtrium0." + str(i) + ".csv"
    filenameOut = "fireAtrium0." + str(i) + ".bytes"
    csvToBin(filenameIn, filenameOut)