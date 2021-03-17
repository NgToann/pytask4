# import matplotlib.pyplot as plt
# import numpy as np

# x = np.linspace(0, 20, 100)
# plt.plot(x, np.sin(x))
# plt.show()

# msg = "Hello World"
# print(msg)

import requests
import json

apiURL = "https://opendata.datainfogreffe.fr/api/records/1.0/search/?dataset=chiffres-cles-2020&q=&sort=ca_1&facet=denomination&facet=libelle_ape&facet=code_postal&facet=ville&facet=departement&facet=region&facet=greffe&facet=tranche_ca_millesime_1&facet=tranche_ca_millesime_2&facet=tranche_ca_millesime_3"

#responseTest = requests.get("http://api.open-notify.org/astros.json")
# print(response.json())
# print(response.status_code)

def jprint(obj):
    # create a formatted string of the Python JSON object
    text = json.dumps(obj, sort_keys=False, indent=4)
    print(text)

respone = requests.get(apiURL)
jsonData = respone.json()
result = []
print("number of record: {}".format(len(jsonData['records'])))
for item in jsonData['records']:
    result.append(item['fields']['siren'])
print(result)

#print(respone.json())
#jprint(responseTest.json())
#jprint(respone.json())
