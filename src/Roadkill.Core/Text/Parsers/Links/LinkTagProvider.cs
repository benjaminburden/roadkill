using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using Ganss.XSS;
using Roadkill.Core.Configuration;
using Roadkill.Core.Database;
using Roadkill.Core.Database.Repositories;
using Roadkill.Core.Text;

namespace Roadkill.Core.Converters
{
    public class LinkTagProvider
    {
        private ApplicationSettings _applicationSettings;
        private static Regex _anchorRegex = new Regex("(?<hash>(#|%23).+)", RegexOptions.IgnoreCase);
        private readonly IPageRepository _pageRepository;
        private List<string> _externalLinkPrefixes;

        /// <summary>
        /// Used to resolve the full path of urls for pages in the markup.
        /// </summary>
        public UrlResolver UrlResolver { get; set; }

        public LinkTagProvider(IPageRepository pageRepository)
        {
            _pageRepository = pageRepository;

            _externalLinkPrefixes = new List<string>()
            {
                "http://",
                "https://",
                "www.",
                "mailto:",
                "#",
                "tag:"
            };

            // Create the UrlResolver to resolve all wiki urls
            HttpContextBase httpContext = null;
            if (HttpContext.Current != null)
                httpContext = new HttpContextWrapper(HttpContext.Current);

            UrlResolver = new UrlResolver(httpContext);
        }

        /// <summary>
        /// Handles internal links, and the 'attachment:' prefix for attachment links.
        /// </summary>
        public HtmlLinkTag LinkParsed(HtmlLinkTag htmlLinkTag)
        {
            if (!_externalLinkPrefixes.Any(x => htmlLinkTag.OriginalHref.StartsWith(x)))
            {
                htmlLinkTag.IsInternalLink = true;

                // Parse internal links, including attachments, tag: and special: links
                string href = htmlLinkTag.OriginalHref;
                string lowerHref = href.ToLower();

                if (lowerHref.StartsWith("attachment:") || lowerHref.StartsWith("~/"))
                {
                    ConvertAttachmentHrefToFullPath(htmlLinkTag);
                }
                else if (lowerHref.StartsWith("special:"))
                {
                    ConvertSpecialPageHrefToFullPath(htmlLinkTag);
                }
                else
                {
                    ConvertInternalLinkHrefToFullPath(htmlLinkTag);
                }
            }
            else
            {
                // Add the external-link class to all outward bound links, 
                // except for anchors pointing to <a name=""> tags on the current page.
                if (!htmlLinkTag.OriginalHref.StartsWith("#"))
                    htmlLinkTag.CssClass = "external-link";
            }

            return htmlLinkTag;
        }

        /// <summary>
        /// Updates the LinkEventArgs.Href to be a full path to the attachment
        /// </summary>
        private void ConvertAttachmentHrefToFullPath(HtmlLinkTag htmlLinkTag)
        {
            string href = htmlLinkTag.OriginalHref;
            string lowerHref = href.ToLower();

            if (lowerHref.StartsWith("attachment:"))
            {
                // Remove the attachment: part
                href = href.Remove(0, 11);
                if (!href.StartsWith("/"))
                    href = "/" + href;
            }
            else if (lowerHref.StartsWith("~/"))
            {
                // Remove the ~ 
                href = href.Remove(0, 1);
            }

            // Get the full path to the attachment
            string attachmentsPath = _applicationSettings.AttachmentsUrlPath;
            htmlLinkTag.Href = UrlResolver.ConvertToAbsolutePath(attachmentsPath) + href;
        }

        /// <summary>
        /// Updates the LinkEventArgs.Href to be a full path to the Special: page
        /// </summary>
        private void ConvertSpecialPageHrefToFullPath(HtmlLinkTag htmlLinkTag)
        {
            string href = htmlLinkTag.OriginalHref;
            htmlLinkTag.Href = UrlResolver.ConvertToAbsolutePath("~/wiki/" + href);
        }

        /// <summary>
        /// Updates the LinkEventArgs.Href to be a full path to the page, and the CssClass
        /// </summary>
        private void ConvertInternalLinkHrefToFullPath(HtmlLinkTag htmlLinkTag)
        {
            string href = htmlLinkTag.OriginalHref;

            // Parse internal links
            string title = href;
            string anchorHash = "";

            // Parse anchors for other pages
            if (_anchorRegex.IsMatch(href))
            {
                // Grab the hash contents
                Match match = _anchorRegex.Match(href);
                anchorHash = match.Groups["hash"].Value;

                // Grab the url
                title = href.Replace(anchorHash, "");
            }

            // For markdown, only urls with "-" in them are valid, spaces are ignored.
            // Remove these, so a match is made. No url has a "-" in, so replacing them is ok.
            title = title.Replace("-", " ");

            // Find the page, or if it doesn't exist point to the new page url
            Page page = _pageRepository.GetPageByTitle(title);
            if (page != null)
            {
                href = UrlResolver.GetInternalUrlForTitle(page.Id, page.Title);
                href += anchorHash;
            }
            else
            {
                href = UrlResolver.GetNewPageUrlForTitle(href);
                htmlLinkTag.CssClass = "missing-page-link";
            }

            htmlLinkTag.Href = href;
            htmlLinkTag.Target = "";
        }
    }
}