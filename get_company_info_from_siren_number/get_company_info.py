import requests
import json
from operator import attrgetter
from variables import URL_GET_DATASET, URL_GET_DATASETS_PAR, URL_GET_RECORDS, SORT_BY

def get_content_from_api(url):
    try:
        respone = requests.get(url)
        if respone.status_code != 200:
            data = respone.json()
            return False
        data = respone.json()
        return data
    except:
        return False

def getCompanyInfo (siren_search):
    hasData = False
    data = []
    # get number of datasets (default = 10)
    dataset = get_content_from_api(URL_GET_DATASET)
    dtset_number = dataset['nhits'] if dataset else 10
    print('total datasets: ', dtset_number)

    # get datasets content
    content_datasets = get_content_from_api(URL_GET_DATASETS_PAR.format(dtset_number))
    datasets = content_datasets['datasets'] if content_datasets else []
    
    for dtset in datasets:
        id = dtset['datasetid']
        print('getting datasetid: ', id)
        #get records content by datasetid and siren number
        content_dtset = get_content_from_api(URL_GET_RECORDS.format(id, siren_search))
        if content_dtset == False:
            continue
        no_of_record = content_dtset['nhits']
        if no_of_record == 0:
            continue
        record = content_dtset['records']
        fields = record[0]['fields']
        siren_number = fields['siren']
        if siren_number == siren_search:
            print('siren: {} found at dataset: {}'.format(siren_search, record[0]['datasetid']))
            hasData = True
            data.append(fields)
    
    if hasData:
        # get the newest information, sort by date_de_publication
        print('get company info sucessful !')
        data = sorted(data, key=lambda k: k.get(SORT_BY, 0), reverse=True)
        return data[0]
    else:
        print('company not found !')
        return False






