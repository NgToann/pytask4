import os
import sys
import json
import argparse
from get_company_info import getCompanyInfo

# Export to json file
def export_data(data, siren_number):
    cwd = os.getcwd()
    file_path = os.path.join(cwd, 'json_output', f'{siren_number}.json')
    with open(file_path, 'w') as f:
        json.dump(data, f)
    print('Export data to JSON successfully!')

# excute with parameter
# main.py -s [siren_number]
if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('-s', '--siren', help='siren number', required=True)

    # get siren number
    args = vars(parser.parse_args())
    siren_number = args['siren']

    # get company info
    info = getCompanyInfo(siren_number)

    # respone json file
    if info:
        export_data(info, siren_number)

