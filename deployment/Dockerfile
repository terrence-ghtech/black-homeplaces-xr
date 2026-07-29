FROM nginx:alpine

COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY webgl/ /usr/share/nginx/html/

# Normalize permissions so nginx can always read Unity assets
RUN find /usr/share/nginx/html -type d -exec chmod 755 {} \; \
 && find /usr/share/nginx/html -type f -exec chmod 644 {} \;

EXPOSE 8080

STOPSIGNAL SIGQUIT

CMD ["nginx", "-g", "daemon off;"]
