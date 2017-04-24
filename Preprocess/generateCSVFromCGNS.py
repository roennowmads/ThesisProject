from paraview import simple
import os, os.path

if __name__ == '__main__':
    directory = 'C:\Users\madsr\Desktop\Oil Rig Data'
    files = [name for name in os.listdir(directory) if os.path.isfile(os.path.join(directory, name))]

    for i, file in enumerate(files):
        print "Processing: " + file
        reader = simple.OpenDataFile(directory + "/" + file)
        reader.PointArrayStatus = ['CH4methaneIG.MassFraction']
        #filename = os.path.splitext(file)[0]
        writer = simple.CreateWriter(directory + "/output/" + "frame" + str(i) + ".csv", reader)
        writer.WriteAllTimeSteps = 1
        writer.FieldAssociation = "Points"
        writer.UpdatePipeline()
        simple.Delete() #Avoids memory leak
        print "File processed: " + file
        
    
#Multiprocessing version (doesn't seem to work with pvpython):
'''from paraview import simple
import os, os.path
from multiprocessing import Process

def f(files, startI, endI):
    numberOfFiles = len(files)
    for i in range(startI, endI):
        file = files[i]
        reader = simple.OpenDataFile(directory + "/" + file)
        reader.PointArrayStatus = ['Visibility']
        filename = os.path.splitext(file)[0]
        writer = simple.CreateWriter(directory + "/output/" + filename + ".csv", reader)
        writer.WriteAllTimeSteps = 1
        writer.FieldAssociation = "Points"
        writer.UpdatePipeline()
        simple.Delete() #Avoids memory leak
        print "File processed: " + file
    
if __name__ == '__main__':
    directory = 'atrium data part 2'
    files = [name for name in os.listdir(directory) if os.path.isfile(os.path.join(directory, name))]
    

    p1 = Process(target=f, args=(files, 0, 20))
    p1.start()
    
    p2 = Process(target=f, args=(files, 20, 40))
    p2.start()
    
    p3 = Process(target=f, args=(files, 40, 60))
    p3.start()
    
    p4 = Process(target=f, args=(files, 80, 100))
    p4.start()
    
    p1.join()
    p2.join()
    p3.join()
    p4.join()
'''