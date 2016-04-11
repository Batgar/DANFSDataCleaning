using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DANFS.Services
{
    public class RegexDateParser
    {
        private void GetDates()
        {
            //The master regex to pull all dates out of the content:
            /*
            
            //Gets everything but dates like 10 August -- Working on that.... 
             (\b\d{1,2}\D{0,3})?\b(?:Jan(?:uary)?|Feb(?:ruary)?|Mar(?:ch)?|Apr(?:il)?|May|Jun(?:e)?|Jul(?:y)?|Aug(?:ust)?|Sep(?:tember)?|Oct(?:ober)?|(Nov|Dec)(?:ember)?)\D?(\d{1,2}\D?)?\D?((19[7-9]\d|20\d{2})|\d{2})

            Follow the above with a /g to get them across the whole document and not just stop at the first result.



             
              
             */
        }
    }
}
