
from get_company_info import getCompanyInfo

siren_search = "315334011"
info = getCompanyInfo(siren_search)
if info:
    print(info)