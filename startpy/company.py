import requests
import json


apiGetDatasets = "https://opendata.datainfogreffe.fr/api/datasets/1.0/search/?q="
apiGetRecords = "https://opendata.datainfogreffe.fr/api/v1/console/records/1.0/search/?"
# pass datasetid, total records
par = "dataset={}&rows={}"

def jprint(obj):
    # create a formatted string of the Python JSON object
    text = json.dumps(obj, sort_keys=False, indent=4)
    print(text)

responeDatasets = requests.get(apiGetDatasets)
datasets = responeDatasets.json()['datasets']

for dtset in datasets:
    datsetId = dtset['datasetid']
    noOfRows = dtset['nhits']
    print(datsetId)
    print(noOfRows)
    # get api record with par
    #responseRecords = requests.get(apiGetRecords.format(datsetId, noOfRows))
    #records = responseRecords['records']
    # for record in records:
    #     if record['']


# result = []
# print("number of record: {}".format(len(jsonData['records'])))
# for item in jsonData['records']:
#     result.append(item['fields']['siren'])
# print(result)