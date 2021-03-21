
import json
import uuid

def jprint(obj):
    # create a formatted string of the Python JSON object
    text = json.dumps(obj, sort_keys=False, indent=4)
    print(text)

f = open('json_input/' + 'device.json')
fake = []
data = json.load(f)
f.close()
fake.append(data)
for i in range(1, 6):
    data = {'deviceId':"{}".format(uuid.uuid4()), 'deviceName':'Name{}'.format(i * 11)}
    fake.append(data)
jprint(fake)