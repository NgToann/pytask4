import requests
import json

apiGetDatasets = "https://opendata.datainfogreffe.fr/api/datasets/1.0/search"
apiGetDatasetsByPar = "https://opendata.datainfogreffe.fr/api/datasets/1.0/search/?q=&rows={}"
apiGetRecord = "https://opendata.datainfogreffe.fr/api/records/1.0/search/?dataset={}&q=siren%3D{}&facet=denomination&facet=libelle_ape&facet=code_postal&facet=ville&facet=departement&facet=region&facet=greffe&facet=tranche_ca_millesime_1&facet=tranche_ca_millesime_2&facet=tranche_ca_millesime_3"

def jprint(obj):
    # create a formatted string of the Python JSON object
    text = json.dumps(obj, sort_keys=False, indent=4)
    print(text)

def contentFromAPI(url):
    respone = requests.get(url)
    if respone.status_code != 200:
        return None
    return respone.json()

sirenSearch = "844760389"
# get no of dataset
responeDatasets = contentFromAPI(apiGetDatasets)
noOfDatasets = responeDatasets['nhits'] if responeDatasets != None else 10

# get datasets with row = par
responeDatasets = contentFromAPI(apiGetDatasetsByPar.format(noOfDatasets))

datasets = responeDatasets['datasets'] if responeDatasets != None else []
for dtset in datasets:
    datasetid = dtset['datasetid']
    
    responeData = contentFromAPI(apiGetRecord.format(datasetid, sirenSearch))
    if responeData == None:
        continue
    noOfRecord = responeData['nhits']
    if noOfRecord == 0:
        continue
    records = responeData['records']
    fields = records[0]['fields']
    sirenNo = fields['siren']

    if sirenNo == sirenSearch:        
        print(datasetid)
        print("nhits: {}".format(noOfRecord))    
        name = fields['denomination']
        print(sirenNo)
        print(name)
        jprint(responeData)
        break

