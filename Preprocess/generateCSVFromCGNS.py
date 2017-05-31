from paraview import simple, vtk
import os, os.path
import preprocess

if __name__ == '__main__':
    directory = 'C:/Users/Mads/Desktop/fireball'
    files = [name for name in os.listdir(directory) if os.path.isfile(os.path.join(directory, name))]
    
    rangeMin = float("inf")
    rangeMax = float("-inf")
    numberOfPoints = 0
    
    attribute = "Temperature"

    for i, file in enumerate(files):
        print "Processing: " + file
        reader = simple.OpenDataFile(directory + "/" + file)
        reader.PointArrayStatus = [attribute]
        reader.UpdatePipeline()
        info = reader.GetDataInformation().DataInformation
        arrayInfo = info.GetArrayInformation(attribute, vtk.vtkDataObject.FIELD_ASSOCIATION_POINTS)
        numberOfPoints = arrayInfo.GetNumberOfTuples()
        range = arrayInfo.GetComponentRange(0)  #all the cgns files need to be loaded in in order to get the real range.
        if rangeMin > range[0]:
            rangeMin = range[0]
        
        if rangeMax < range[1]:
            rangeMax = range[1]
        
        writer = simple.CreateWriter(directory + "/output/" + "frame" + str(i) + ".csv", reader)
        writer.WriteAllTimeSteps = 1
        writer.FieldAssociation = "Points"
        writer.UpdatePipeline()
        simple.Delete() #Avoids memory leak
        print "File processed: " + file
        
    print rangeMin, rangeMax, numberOfPoints

    #Run preprocess directly after:
    preprocess.runPreprocess(directory, rangeMin, rangeMax, numberOfPoints)
